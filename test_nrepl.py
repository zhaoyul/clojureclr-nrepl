#!/usr/bin/env python3
"""
Test script for ClojureCLR nREPL Server
Tests core operations and middleware functionality
"""

import socket
import sys
import time


def bencode_encode(obj):
    """Bencode encoder supporting strings, ints, lists, dicts"""
    if isinstance(obj, str):
        b = obj.encode('utf-8')
        return f"{len(b)}:".encode('utf-8') + b
    elif isinstance(obj, int):
        return f"i{obj}e".encode('utf-8')
    elif isinstance(obj, list):
        result = b'l'
        for item in obj:
            result += bencode_encode(item)
        result += b'e'
        return result
    elif isinstance(obj, dict):
        result = b'd'
        for k, v in obj.items():
            result += bencode_encode(k)
            result += bencode_encode(v)
        result += b'e'
        return result
    else:
        return bencode_encode(str(obj))


def bencode_decode(data, offset=0):
    """Bencode decoder"""
    if offset >= len(data):
        return None, offset
    
    c = data[offset]
    
    if c == ord('d'):
        result = {}
        offset += 1
        while offset < len(data) and data[offset] != ord('e'):
            key, offset = bencode_decode(data, offset)
            value, offset = bencode_decode(data, offset)
            result[key] = value
        return result, offset + 1
    elif c == ord('l'):
        result = []
        offset += 1
        while offset < len(data) and data[offset] != ord('e'):
            item, offset = bencode_decode(data, offset)
            result.append(item)
        return result, offset + 1
    elif c == ord('i'):
        end = data.find(b'e', offset)
        num = int(data[offset+1:end])
        return num, end + 1
    elif ord('0') <= c <= ord('9'):
        colon = data.find(b':', offset)
        length = int(data[offset:colon])
        string_data = data[colon+1:colon+1+length]
        try:
            return string_data.decode('utf-8'), colon + 1 + length
        except:
            return string_data, colon + 1 + length
    else:
        return None, offset + 1


def read_all_responses(sock, timeout=3):
    """Read all available responses from socket"""
    sock.settimeout(timeout)
    all_data = b''
    
    try:
        while True:
            data = sock.recv(8192)
            if not data:
                break
            all_data += data
    except socket.timeout:
        pass
    
    responses = []
    offset = 0
    while offset < len(all_data):
        try:
            response, new_offset = bencode_decode(all_data, offset)
            if response:
                responses.append(response)
                offset = new_offset
            else:
                break
        except:
            break
    
    return responses


class NReplTest:
    def __init__(self, host='127.0.0.1', port=1667):
        self.host = host
        self.port = port
        self.sock = None
        self.session = None
        self.passed = 0
        self.failed = 0
    
    def connect(self):
        """Connect to nREPL server"""
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(10)
        self.sock.connect((self.host, self.port))
        print(f"✓ Connected to {self.host}:{self.port}")
    
    def send(self, msg):
        """Send message"""
        self.sock.send(bencode_encode(msg))
    
    def recv(self, timeout=3):
        """Receive responses"""
        return read_all_responses(self.sock, timeout)
    
    def test_clone(self):
        """Test clone operation"""
        print("\n=== Test: clone ===")
        self.send({"op": "clone", "id": 1})
        responses = self.recv()
        
        if responses and "new-session" in responses[0]:
            self.session = responses[0]["new-session"]
            print(f"✓ Got session: {self.session[:8]}...")
            self.passed += 1
            return True
        else:
            print("✗ No session received")
            self.failed += 1
            return False
    
    def test_describe(self):
        """Test describe operation"""
        print("\n=== Test: describe ===")
        self.send({"op": "describe", "id": 2, "session": self.session})
        responses = self.recv()
        
        if responses and "ops" in responses[0]:
            ops = responses[0]["ops"]
            print(f"✓ Server supports {len(ops)} operations")
            for op in sorted(ops.keys()):
                print(f"  - {op}")
            self.passed += 1
            return True
        else:
            print("✗ No operations list")
            self.failed += 1
            return False
    
    def test_eval(self, code, expected_contains=None):
        """Test eval operation"""
        print(f"\n=== Test: eval {code[:30]}... ===")
        self.send({"op": "eval", "id": 3, "session": self.session, "code": code})
        responses = self.recv(timeout=5)
        
        values = [r.get("value", "") for r in responses if "value" in r]
        if values:
            print(f"✓ Result: {values[0][:50]}...")
            if expected_contains and expected_contains not in str(values):
                print(f"✗ Expected '{expected_contains}' not found")
                self.failed += 1
                return False
            self.passed += 1
            return True
        else:
            print("✗ No value returned")
            self.failed += 1
            return False
    
    def test_complete(self, symbol, ns="clojure.core"):
        """Test complete middleware"""
        print(f"\n=== Test: complete '{symbol}' ===")
        self.send({"op": "complete", "id": 4, "session": self.session, 
                   "symbol": symbol, "ns": ns})
        responses = self.recv()
        
        if responses and "completions" in responses[0]:
            completions = responses[0]["completions"]
            print(f"✓ Found {len(completions)} completions")
            for c in completions[:3]:
                print(f"  - {c.get('candidate')} ({c.get('type')})")
            self.passed += 1
            return True
        else:
            print("✗ No completions")
            self.failed += 1
            return False
    
    def test_info(self, symbol, ns="clojure.core"):
        """Test info middleware"""
        print(f"\n=== Test: info '{symbol}' ===")
        self.send({"op": "info", "id": 5, "session": self.session,
                   "symbol": symbol, "ns": ns})
        responses = self.recv()
        
        if responses and "name" in responses[0]:
            r = responses[0]
            print(f"✓ Name: {r.get('name')}")
            print(f"  NS: {r.get('ns')}")
            print(f"  Args: {r.get('arglists', 'N/A')[:50]}...")
            self.passed += 1
            return True
        else:
            print("✗ No info returned")
            self.failed += 1
            return False
    
    def test_eldoc(self, symbol, ns="clojure.core"):
        """Test eldoc middleware"""
        print(f"\n=== Test: eldoc '{symbol}' ===")
        self.send({"op": "eldoc", "id": 6, "session": self.session,
                   "sym": symbol, "ns": ns})
        responses = self.recv()
        
        if responses:
            r = responses[0]
            if "eldoc" in r:
                eldoc = r["eldoc"]
                print(f"✓ Found {len(eldoc)} signatures")
                for sig in eldoc[:2]:
                    print(f"  ({' '.join(sig)})")
                self.passed += 1
                return True
            elif "no-eldoc" in r.get("status", []):
                print("✓ No eldoc (expected for invalid symbol)")
                self.passed += 1
                return True
        
        print("✗ Unexpected response")
        self.failed += 1
        return False
    
    def test_in_ns(self):
        """Test namespace switching"""
        print("\n=== Test: in-ns ===")
        self.send({"op": "eval", "id": 7, "session": self.session,
                   "code": "(in-ns 'clojure.string)"})
        responses = self.recv()
        
        self.send({"op": "eval", "id": 8, "session": self.session,
                   "code": "(ns-name *ns*)"})
        responses = self.recv()
        
        values = [r.get("value", "") for r in responses if "value" in r]
        if "clojure.string" in str(values):
            print("✓ Successfully switched to clojure.string")
            
            # Test that clojure.core functions are still available
            self.send({"op": "eval", "id": 9, "session": self.session,
                       "code": "(str \"hello \" (+ 1 2))"})
            responses = self.recv()
            values = [r.get("value", "") for r in responses if "value" in r]
            if '"hello 3"' in str(values):
                print("✓ clojure.core functions available via refer")
                self.passed += 1
                return True
        
        print("✗ Namespace switch failed")
        self.failed += 1
        return False
    
    def close(self):
        """Close connection"""
        if self.sock:
            self.sock.close()
    
    def summary(self):
        """Print test summary"""
        print("\n" + "="*50)
        print(f"Tests Passed: {self.passed}")
        print(f"Tests Failed: {self.failed}")
        print("="*50)
        return self.failed == 0


def main():
    print("="*50)
    print("ClojureCLR nREPL Server Test Suite")
    print("="*50)
    
    test = NReplTest()
    
    try:
        test.connect()
        
        # Core operations
        test.test_clone()
        test.test_describe()
        
        # Eval tests
        test.test_eval("(+ 1 2 3)", "6")
        test.test_eval('(str "hello" " world")', "hello world")
        
        # Middleware tests
        test.test_complete("map")
        test.test_complete("str/join")  # Namespace shorthand
        test.test_info("reduce")
        test.test_eldoc("map")
        test.test_eldoc("xyz123")  # Invalid symbol
        
        # Namespace switching
        test.test_in_ns()
        
    except Exception as e:
        print(f"\n✗ Test error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        test.close()
    
    success = test.summary()
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
