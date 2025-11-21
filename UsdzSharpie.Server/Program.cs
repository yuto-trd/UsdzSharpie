using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenTK.Mathematics;
using UsdzSharpie.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<RendererService>();

var app = builder.Build();
var rendererService = app.Services.GetRequiredService<RendererService>();

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

app.MapPost("/convert-to-obj", async ([FromForm] IFormFile usdzFile) =>
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

        // Load USDZ with Assimp
        using var loader = new AssimpLoader();
        var scene = loader.LoadUsdz(tempPath);

        // Convert to OBJ/MTL/textures ZIP
        var modelName = Path.GetFileNameWithoutExtension(usdzFile.FileName) ?? "model";
        var zipData = ObjExporter.ExportToZip(scene, modelName);

        // Return ZIP file
        return Results.File(zipData, "application/zip", $"{modelName}.zip");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to convert USDZ to OBJ: {ex.Message}");
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
.WithName("ConvertToObj")
.DisableAntiforgery();

app.MapPost("/convert-to-gltf", async ([FromForm] IFormFile usdzFile) =>
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

        // Load USDZ with Assimp
        using var loader = new AssimpLoader();
        var scene = loader.LoadUsdz(tempPath);

        // Convert to glTF/bin/textures ZIP
        var modelName = Path.GetFileNameWithoutExtension(usdzFile.FileName) ?? "model";
        var zipData = GltfExporter.ExportToZip(scene, modelName);

        // Return ZIP file
        return Results.File(zipData, "application/zip", $"{modelName}.zip");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to convert USDZ to glTF: {ex.Message}");
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
.WithName("ConvertToGltf")
.DisableAntiforgery();

app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>USDZ Renderer Server</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 900px; margin: 50px auto; padding: 20px; }
        h1 { color: #333; }
        h2 { color: #555; border-bottom: 2px solid #007bff; padding-bottom: 10px; margin-top: 30px; }
        form { background: #f5f5f5; padding: 20px; border-radius: 8px; margin-bottom: 20px; }
        label { display: block; margin: 10px 0 5px; font-weight: bold; }
        input, select, textarea { width: 100%; padding: 8px; margin-bottom: 15px; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }
        button { background: #007bff; color: white; padding: 10px 20px; border: none; border-radius: 4px; cursor: pointer; }
        button:hover { background: #0056b3; }
        .result { margin-top: 20px; padding: 15px; background: #fff; border: 1px solid #ddd; border-radius: 4px; }
        img { max-width: 100%; border: 1px solid #ddd; border-radius: 4px; }
        .section { margin-bottom: 40px; }
    </style>
</head>
<body>
    <h1>USDZ Converter & Renderer Server</h1>
    <p>Upload a USDZ file to convert it to OBJ or glTF format, or render images from camera viewpoints.</p>

    <div class=""section"">
        <h2>Convert to OBJ</h2>
        <p>Convert USDZ to OBJ/MTL with textures (as ZIP file)</p>
        <form id=""convertForm"">
            <label>USDZ File:</label>
            <input type=""file"" id=""convertUsdzFile"" accept="".usdz"" required>
            <button type=""submit"">Convert to OBJ</button>
        </form>
        <div id=""convertResult"" class=""result"" style=""display: none;""></div>
    </div>

    <div class=""section"">
        <h2>Convert to glTF</h2>
        <p>Convert USDZ to glTF 2.0 format with textures (as ZIP file)</p>
        <form id=""convertGltfForm"">
            <label>USDZ File:</label>
            <input type=""file"" id=""convertGltfUsdzFile"" accept="".usdz"" required>
            <button type=""submit"">Convert to glTF</button>
        </form>
        <div id=""convertGltfResult"" class=""result"" style=""display: none;""></div>
    </div>

    <div class=""section"">
        <h2>Render Image</h2>
        <p>Render USDZ file from specified camera viewpoint</p>
        <form id=""renderForm"">
            <label>USDZ File:</label>
            <input type=""file"" id=""renderUsdzFile"" accept="".usdz"" required>

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
        <div id=""renderResult"" class=""result"" style=""display: none;""></div>
    </div>

    <script>
        document.getElementById('convertForm').onsubmit = async (e) => {
            e.preventDefault();

            const formData = new FormData();
            formData.append('usdzFile', document.getElementById('convertUsdzFile').files[0]);

            const result = document.getElementById('convertResult');
            result.style.display = 'block';
            result.innerHTML = '<p>Converting...</p>';

            try {
                const response = await fetch('/convert-to-obj', {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    const filename = response.headers.get('Content-Disposition')?.match(/filename=""?([^""]+)""?/)?.[1] || 'model.zip';

                    result.innerHTML = '<p style=""color: green;"">Conversion successful!</p>' +
                                      '<a href=""' + url + '"" download=""' + filename + '"" style=""display: inline-block; margin-top: 10px; padding: 10px 20px; background: #28a745; color: white; text-decoration: none; border-radius: 4px;"">Download ZIP</a>';
                } else {
                    const text = await response.text();
                    result.innerHTML = '<p style=""color: red;"">Error: ' + text + '</p>';
                }
            } catch (error) {
                result.innerHTML = '<p style=""color: red;"">Error: ' + error.message + '</p>';
            }
        };

        document.getElementById('convertGltfForm').onsubmit = async (e) => {
            e.preventDefault();

            const formData = new FormData();
            formData.append('usdzFile', document.getElementById('convertGltfUsdzFile').files[0]);

            const result = document.getElementById('convertGltfResult');
            result.style.display = 'block';
            result.innerHTML = '<p>Converting...</p>';

            try {
                const response = await fetch('/convert-to-gltf', {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    const filename = response.headers.get('Content-Disposition')?.match(/filename=""?([^""]+)""?/)?.[1] || 'model.zip';

                    result.innerHTML = '<p style=""color: green;"">Conversion successful!</p>' +
                                      '<a href=""' + url + '"" download=""' + filename + '"" style=""display: inline-block; margin-top: 10px; padding: 10px 20px; background: #28a745; color: white; text-decoration: none; border-radius: 4px;"">Download ZIP</a>';
                } else {
                    const text = await response.text();
                    result.innerHTML = '<p style=""color: red;"">Error: ' + text + '</p>';
                }
            } catch (error) {
                result.innerHTML = '<p style=""color: red;"">Error: ' + error.message + '</p>';
            }
        };

        document.getElementById('renderForm').onsubmit = async (e) => {
            e.preventDefault();

            const formData = new FormData();
            formData.append('usdzFile', document.getElementById('renderUsdzFile').files[0]);
            formData.append('viewpointJson', document.getElementById('viewpoint').value);

            const result = document.getElementById('renderResult');
            result.style.display = 'block';
            result.innerHTML = '<p>Rendering...</p>';

            try {
                const response = await fetch('/render', {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    result.innerHTML = '<h3>Rendered Image:</h3><img src=""' + url + '"">';
                } else {
                    const text = await response.text();
                    result.innerHTML = '<p style=""color: red;"">Error: ' + text + '</p>';
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
