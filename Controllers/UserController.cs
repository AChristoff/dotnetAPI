using Microsoft.AspNetCore.Mvc;
using DotnetAPI.Models;

namespace DotnetAPI.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    DataContextDapper _dapper;
    public UserController(IConfiguration config)
    {
        _dapper = new DataContextDapper(config);
    }

    /**
    *
    * GET all USERS
    *
    * GET: /api/users
    * @return IEnumerable<User>
    *
    */
    [HttpGet("/api/users")]
    public IEnumerable<User> GetUsers()
    {
        string sql = @"
            SELECT [UserId],
                [FirstName],
                [LastName],
                [Email],
                [Gender],
                [Active] 
            FROM TutorialAppSchema.Users";
        IEnumerable<User> users = _dapper.LoadData<User>(sql);
        return users;
    }


    /**
    *
    * GET USER by ID
    *
    * GET: /api/users/{id}
    * @param int id
    * @return User
    *
    */
    [HttpGet("{id}")]
    public User GetSingleUser(int id)
    {
        string sql = @"
            SELECT [UserId],
                [FirstName],
                [LastName],
                [Email],
                [Gender],
                [Active] 
            FROM TutorialAppSchema.Users
            WHERE UserId = " + id.ToString(); //"7"
        User user = _dapper.LoadDataSingle<User>(sql);
        return user;
    }

    /**
    *
    * EDIT USER
    *
    * POST: /api/users
    * @param User user
    * @return IActionResult
    *
    */

    [HttpPut]
    public IActionResult EditUser(User user)
    {
        string sql = @"
            UPDATE TutorialAppSchema.Users
            SET [FirstName] = '" + user.FirstName +
                "', [LastName] = '" + user.LastName +
                "', [Email] = '" + user.Email +
                "', [Gender] = '" + user.Gender +
                "', [Active] = '" + user.Active +
            "' WHERE UserId = " + user.UserId;

        Console.WriteLine(sql);

        if (_dapper.ExecuteSql(sql))
        {
            return Ok();
        }

        throw new Exception("Failed to Update User");
    }

    /**
    *
    * CREATE USER
    *
    * POST: /api/users
    * @param User user
    * @return IActionResult
    *
    */
    [HttpPost]
    public IActionResult AddUser(User user)
    {
        string sql = @"
            INSERT INTO TutorialAppSchema.Users(
                [FirstName],
                [LastName],
                [Email],
                [Gender],
                [Active]
            ) VALUES (" +
                "'" + user.FirstName +
                "', '" + user.LastName +
                "', '" + user.Email +
                "', '" + user.Gender +
                "', '" + user.Active +
            "')";

        Console.WriteLine(sql);

        if (_dapper.ExecuteSql(sql))
        {
            return Ok();
        }

        throw new Exception("Failed to Add User");
    }

    /**
    *
    * DELETE USER
    *
    * DELETE: /api/users/{userId}
    * @param int userId
    * @return IActionResult
    *
    */
    [HttpDelete("{id}")]
    public IActionResult DeleteUser(int id)
    {
        string sql = @"
            DELETE FROM TutorialAppSchema.Users 
            WHERE UserId = " + id.ToString();

        Console.WriteLine(sql);

        if (_dapper.ExecuteSql(sql))
        {
            return Ok();
        }

        throw new Exception("Failed to Delete User");
    }
}
