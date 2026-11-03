namespace MCP.Services;

public class AccessModeProvider : IAccessModeProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccessModeProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AccessMode GetAccessMode()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context to resolve an access mode from.");

        if (httpContext.Items.TryGetValue("AccessMode", out var mode) && mode is AccessMode accessMode)
            return accessMode;

        return AccessMode.ReadOnly;
    }
}
