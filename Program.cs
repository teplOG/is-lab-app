using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var appName = app.Configuration["App:Name"] ?? "IsLabApp";
var appVersion = app.Configuration["App:Version"] ?? "0.0.0";

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    time = DateTime.UtcNow
}));

app.MapGet("/version", () => Results.Ok(new
{
    name = appName,
    version = appVersion
}));

var notes = new ConcurrentDictionary<int, Note>();
var lastId = 0;

app.MapPost("/api/notes", (NoteInput input) =>
{
    var error = NoteRules.Validate(input.Title);
    if (error is not null)
    {
        return Results.BadRequest(new { error });
    }

    var id = Interlocked.Increment(ref lastId);
    var note = new Note(id, input.Title.Trim(), input.Text, DateTime.UtcNow);
    notes[id] = note;

    return Results.Created($"/api/notes/{id}", note);
});

app.MapGet("/api/notes", () => Results.Ok(notes.Values.OrderBy(n => n.Id)));

app.MapGet("/api/notes/{id:int}", (int id) =>
    notes.TryGetValue(id, out var note) ? Results.Ok(note) : Results.NotFound());

app.MapDelete("/api/notes/{id:int}", (int id) =>
    notes.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound());

app.MapGet("/db/ping", async () =>
{
    var connectionString = app.Configuration.GetConnectionString("Mssql");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Json(new
        {
            status = "error",
            message = "ConnectionStrings:Mssql is not configured"
        }, statusCode: 503);
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT @@VERSION", connection);
        var serverVersion = (string?)await command.ExecuteScalarAsync();

        return Results.Ok(new
        {
            status = "ok",
            server = connection.DataSource,
            database = connection.Database,
            version = serverVersion?.Split('\n')[0].Trim()
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "error",
            message = ex.Message
        }, statusCode: 503);
    }
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    return Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record Note(int Id, string Title, string? Text, DateTime CreatedAt);

record NoteInput(string Title, string? Text);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
