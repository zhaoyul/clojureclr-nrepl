using System;
using System.IO;
using System.Reflection;
using System.Text;
using clojure.lang;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

// Initialize ClojureCLR before ASP.NET loads additional assemblies.
var baseDir = AppContext.BaseDirectory;
foreach (var path in Directory.GetFiles(baseDir, "clojure.*.dll"))
{
    try { Assembly.LoadFrom(path); } catch { }
}

var cljFile = Path.Combine(baseDir, "src", "demo", "minimal.clj");
var loadFile = RT.var("clojure.core", "load-file");
loadFile.invoke(cljFile);

var require = RT.var("clojure.core", "require");
require.invoke(Symbol.create("demo.minimal"));

var helloVar = RT.var("demo.minimal", "hello");
var healthVar = RT.var("demo.minimal", "health");
var echoVar = RT.var("demo.minimal", "echo");

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Text((string)helloVar.invoke(), "text/plain; charset=utf-8"));

app.MapGet("/health", () => Results.Text((string)healthVar.invoke(), "application/json; charset=utf-8"));

app.MapPost("/echo", async (HttpRequest req) =>
{
    using var sr = new StreamReader(req.Body, Encoding.UTF8);
    var body = await sr.ReadToEndAsync();
    var result = echoVar.invoke(body);
    return Results.Text((string)result, "text/plain; charset=utf-8");
});

app.Run();
