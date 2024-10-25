using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Logging;

namespace DotnetAPI.Conventions
{
  public class GlobalRoutePrefixConvention : IApplicationModelConvention
  {
    private readonly AttributeRouteModel _globalPrefix;

    public GlobalRoutePrefixConvention(string prefix)
    {
      _globalPrefix = new AttributeRouteModel(new RouteAttribute(prefix));
    }

    public void Apply(ApplicationModel application)
    {
      foreach (var controller in application.Controllers)
      {
        Console.WriteLine($"Applying global prefix to controller: {controller.ControllerName}");
        foreach (var selector in controller.Selectors)
        {
          if (selector.AttributeRouteModel != null)
          {
            selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_globalPrefix, selector.AttributeRouteModel);
          }
          else
          {
            selector.AttributeRouteModel = _globalPrefix;
          }
        }
      }
    }
  }
}
