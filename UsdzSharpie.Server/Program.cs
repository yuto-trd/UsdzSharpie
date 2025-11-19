using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenTK.Mathematics;
using UsdzSharpie.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<RendererService>();

var app = builder.Build();

// Initialize OpenGL context at startup
var rendererService = app.Services.GetRequiredService<RendererService>();
rendererService.Initialize();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/render", async ([FromForm] IFormFile usdzFile, [FromForm] string viewpointJson) =>
{
    if (usdzFile == null || usdzFile.Length == 0)
    {
        return Results.BadRequest("USDZ file is required");
    }

    // Save USDZ file temporarily
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".usdz");
    try
    {
        using (var stream = File.Create(tempPath))
        {
            await usdzFile.CopyToAsync(stream);
        }

        // Parse viewpoint
        CameraViewpoint viewpoint;
        try
        {
            var viewpointDto = JsonSerializer.Deserialize<ViewpointDto>(viewpointJson, JsonSerializerOptions.Web);
            if (viewpointDto == null)
            {
                viewpoint = new CameraViewpoint
                {
                    Position = new Vector3(1, 1, 1),
                    Target = Vector3.Zero,
                    Fov = 45.0f,
                    Width = 800,
                    Height = 600,
                    EnableLighting = true
                };
            }
            else
            {
                viewpoint = new CameraViewpoint
                {
                    Position = new Vector3(viewpointDto.PositionX, viewpointDto.PositionY, viewpointDto.PositionZ),
                    Target = new Vector3(viewpointDto.TargetX, viewpointDto.TargetY, viewpointDto.TargetZ),
                    Fov = viewpointDto.Fov,
                    Width = viewpointDto.Width,
                    Height = viewpointDto.Height,
                    EnableLighting = viewpointDto.EnableLighting
                };
            }
        }
        catch
        {
            viewpoint = new CameraViewpoint
            {
                Position = new Vector3(1, 1, 1),
                Target = Vector3.Zero,
                Fov = 45.0f,
                Width = 800,
                Height = 600,
                EnableLighting = true
            };
        }

        // Render
        var imageData = rendererService.Render(tempPath, viewpoint);

        // Return image
        var contentType = "image/png";

        return Results.File(imageData, contentType);
    }
    finally
    {
        // Clean up temp file
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }
})
.WithName("RenderUSDZ")
.DisableAntiforgery();

app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>USDZ Renderer Server</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 50px auto; padding: 20px; }
        h1 { color: #333; }
        form { background: #f5f5f5; padding: 20px; border-radius: 8px; }
        label { display: block; margin: 10px 0 5px; font-weight: bold; }
        input, select, textarea { width: 100%; padding: 8px; margin-bottom: 15px; border: 1px solid #ddd; border-radius: 4px; }
        button { background: #007bff; color: white; padding: 10px 20px; border: none; border-radius: 4px; cursor: pointer; }
        button:hover { background: #0056b3; }
        #result { margin-top: 20px; }
        img { max-width: 100%; border: 1px solid #ddd; border-radius: 4px; }
    </style>
</head>
<body>
    <h1>USDZ Renderer Server</h1>
    <p>Upload a USDZ file and specify camera viewpoints to render images.</p>

    <form id=""uploadForm"">
        <label>USDZ File:</label>
        <input type=""file"" id=""usdzFile"" accept="".usdz"" required>

        <label>Viewpoint (JSON):</label>
        <textarea id=""viewpoint"" rows=""12"">{
  ""positionX"": 1.0,
  ""positionY"": 1.0,
  ""positionZ"": 1.0,
  ""targetX"": 0.0,
  ""targetY"": 0.0,
  ""targetZ"": 0.0,
  ""fov"": 45.0,
  ""width"": 800,
  ""height"": 600,
  ""enableLighting"": true
}</textarea>

        <button type=""submit"">Render</button>
    </form>

    <div id=""result""></div>

    <script>
        document.getElementById('uploadForm').onsubmit = async (e) => {
            e.preventDefault();

            const formData = new FormData();
            formData.append('usdzFile', document.getElementById('usdzFile').files[0]);
            formData.append('viewpointJson', document.getElementById('viewpoint').value);

            const result = document.getElementById('result');
            result.innerHTML = '<p>Rendering...</p>';

            try {
                const response = await fetch('/render', {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    result.innerHTML = '<h2>Result:</h2><img src=""' + url + '"">';
                } else {
                    result.innerHTML = '<p style=""color: red;"">Error: ' + response.statusText + '</p>';
                }
            } catch (error) {
                result.innerHTML = '<p style=""color: red;"">Error: ' + error.message + '</p>';
            }
        };
    </script>
</body>
</html>
", "text/html"));

app.Run();

public class ViewpointDto
{
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    public float Fov { get; set; } = 45.0f;
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public bool EnableLighting { get; set; } = true;
}
