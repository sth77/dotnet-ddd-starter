using Microsoft.AspNetCore.Routing;

namespace App.Api.Common;

/// <summary>
/// One implementation per feature. Discovered by assembly scan in <c>MapApi()</c>, so adding a feature never
/// requires editing the composition root (design §12, option A: no injection points).
/// </summary>
internal interface IEndpointModule
{
    void Map(IEndpointRouteBuilder app);
}
