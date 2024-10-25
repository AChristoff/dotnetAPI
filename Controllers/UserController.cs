
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("users")]
public class UserController : ControllerBase
{
    public UserController()
    {
    }

    [HttpGet]
    public string[] Get()
    {
        string[] responseArray = { "test1", "test2" };
        return responseArray;
    }
}