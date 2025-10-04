using Microsoft.JSInterop;

namespace MeteoMesh5.Shared.Extensions;

public static class IJSRuntimeExtension
{
    public static async ValueTask ToastrSucces(this IJSRuntime jsRt, string message)
    {
        await jsRt.InvokeVoidAsync("ShowToastr", "success", message);
    }
    public static async ValueTask ToastrError(this IJSRuntime jsRt, string message)
    {
        await jsRt.InvokeVoidAsync("ShowToastr", "error", message);
    }
    public static async ValueTask ToastrWarning(this IJSRuntime jsRt, string message)
    {
        await jsRt.InvokeVoidAsync("ShowToastr", "warning", message);
    }

    public static async Task SaveAs(this IJSRuntime jsRt, string filename, byte[] data)
    {
        await jsRt.InvokeAsync<object>(
            "saveAsFile",
            filename,
            Convert.ToBase64String(data));
    }
}