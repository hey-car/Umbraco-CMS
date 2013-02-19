using System.Web.Routing;

namespace Umbraco.Web.Mvc
{
    /// <summary>
	/// A controller factory for the render pipeline of Umbraco. This controller factory tries to create a controller with the supplied
	/// name, and falls back to UmbracoController if none was found.
	/// </summary>
	/// <remarks></remarks>
    public class RenderControllerFactory : UmbracoControllerFactory
	{
		
		/// <summary>
		/// Determines whether this instance can handle the specified request.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns><c>true</c> if this instance can handle the specified request; otherwise, <c>false</c>.</returns>
		/// <remarks></remarks>
		public override bool CanHandle(RequestContext request)
		{
			var dataToken = request.RouteData.DataTokens["area"];
			return dataToken == null || string.IsNullOrWhiteSpace(dataToken.ToString());
		}

	}
}