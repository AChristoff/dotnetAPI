using Microsoft.AspNetCore.Mvc;
using DotnetAPI.Models;
using DotnetAPI.Models.DTOs;
using DotnetAPI.Data;
using AutoMapper;
using Dapper;

namespace DotnetAPI.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private readonly DataContextDapper _dapper;
    private readonly IMapper _mapper;

    public UserController(IConfiguration config, IMapper mapper)
    {
        _dapper = new DataContextDapper(config);
        _mapper = mapper; //_mapper is just for example, not used in this controller
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
    * GET: /api/user/{id}
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
    * POST: /api/user
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
    * POST: /api/user
    * @param User user
    * @return IActionResult
    *
    */
    [HttpPost]
    // public IActionResult AddUser(UserAddDTO userDTO)
    public IActionResult AddUser(UserAddDTO user)
    {
        // var user = _mapper.Map<User>(userDTO);

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
    * DELETE: /api/user/{userId}
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

    /**
    *
    * DELETE MULTIPLE USERS
    *
    * DELETE: /api/users
    * @param int[] ids
    * @return IActionResult
    *
    */
    [HttpDelete("/api/users")]
    public IActionResult DeleteUsers([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return BadRequest("No user IDs provided.");
        }

        // Create parameterized placeholders for each ID
        var parameterNames = string.Join(", ", ids.Select((id, index) => $"@Id{index}"));
        string sql = $@"
        DELETE FROM TutorialAppSchema.Users 
        WHERE UserId IN ({parameterNames})";

        // Define parameters with DynamicParameters to prevent SQL injection
        var parameters = new DynamicParameters();
        for (int i = 0; i < ids.Length; i++)
        {
            parameters.Add($"@Id{i}", ids[i]);
        }

        // Execute the delete operation
        if (_dapper.ExecuteSql(sql, parameters))
        {
            return Ok($"Deleted users with IDs: {string.Join(", ", ids)}");
        }

        // Return error if deletion fails
        throw new Exception("Failed to delete users.");
    }


    // --------------------------------------------------
    //  MORE ENDPOINTS
    // --------------------------------------------------

    [HttpGet("UserSalary/{userId}")]
    public IEnumerable<UserSalary> GetUserSalary(int userId)
    {
        return _dapper.LoadData<UserSalary>(@"
            SELECT UserSalary.UserId
                    , UserSalary.Salary
            FROM  TutorialAppSchema.UserSalary
                WHERE UserId = " + userId.ToString());
    }

    [HttpPost("UserSalary")]
    public IActionResult PostUserSalary(UserSalary userSalaryForInsert)
    {
        string sql = @"
            INSERT INTO TutorialAppSchema.UserSalary (
                UserId,
                Salary
            ) VALUES (" + userSalaryForInsert.UserId.ToString()
                + ", " + userSalaryForInsert.Salary
                + ")";

        if (_dapper.ExecuteSqlWithRowCount(sql) > 0)
        {
            return Ok(userSalaryForInsert);
        }
        throw new Exception("Adding User Salary failed on save");
    }

    [HttpPut("UserSalary")]
    public IActionResult PutUserSalary(UserSalary userSalaryForUpdate)
    {
        string sql = "UPDATE TutorialAppSchema.UserSalary SET Salary="
            + userSalaryForUpdate.Salary
            + " WHERE UserId=" + userSalaryForUpdate.UserId.ToString();

        if (_dapper.ExecuteSql(sql))
        {
            return Ok(userSalaryForUpdate);
        }
        throw new Exception("Updating User Salary failed on save");
    }

    [HttpDelete("UserSalary/{userId}")]
    public IActionResult DeleteUserSalary(int userId)
    {
        string sql = "DELETE FROM TutorialAppSchema.UserSalary WHERE UserId=" + userId.ToString();

        if (_dapper.ExecuteSql(sql))
        {
            return Ok();
        }
        throw new Exception("Deleting User Salary failed on save");
    }

    [HttpGet("UserJobInfo/{userId}")]
    public IEnumerable<UserJobInfo> GetUserJobInfo(int userId)
    {
        return _dapper.LoadData<UserJobInfo>(@"
            SELECT  UserJobInfo.UserId
                    , UserJobInfo.JobTitle
                    , UserJobInfo.Department
            FROM  TutorialAppSchema.UserJobInfo
                WHERE UserId = " + userId.ToString());
    }

    [HttpPost("UserJobInfo")]
    public IActionResult PostUserJobInfo(UserJobInfo userJobInfoForInsert)
    {
        string sql = @"
            INSERT INTO TutorialAppSchema.UserJobInfo (
                UserId,
                Department,
                JobTitle
            ) VALUES (" + userJobInfoForInsert.UserId
                + ", '" + userJobInfoForInsert.Department
                + "', '" + userJobInfoForInsert.JobTitle
                + "')";

        if (_dapper.ExecuteSql(sql))
        {
            return Ok(userJobInfoForInsert);
        }
        throw new Exception("Adding User Job Info failed on save");
    }

    [HttpPut("UserJobInfo")]
    public IActionResult PutUserJobInfo(UserJobInfo userJobInfoForUpdate)
    {
        string sql = "UPDATE TutorialAppSchema.UserJobInfo SET Department='"
            + userJobInfoForUpdate.Department
            + "', JobTitle='"
            + userJobInfoForUpdate.JobTitle
            + "' WHERE UserId=" + userJobInfoForUpdate.UserId.ToString();

        if (_dapper.ExecuteSql(sql))
        {
            return Ok(userJobInfoForUpdate);
        }
        throw new Exception("Updating User Job Info failed on save");
    }

    // [HttpDelete("UserJobInfo/{userId}")]
    // public IActionResult DeleteUserJobInfo(int userId)
    // {
    //     string sql = "DELETE FROM TutorialAppSchema.UserJobInfo  WHERE UserId=" + userId;

    //     if (_dapper.ExecuteSql(sql))
    //     {
    //         return Ok();
    //     }
    //     throw new Exception("Deleting User Job Info failed on save");
    // }

    [HttpDelete("UserJobInfo/{userId}")]
    public IActionResult DeleteUserJobInfo(int userId)
    {
        string sql = @"
            DELETE FROM TutorialAppSchema.UserJobInfo 
                WHERE UserId = " + userId.ToString();

        Console.WriteLine(sql);

        if (_dapper.ExecuteSql(sql))
        {
            return Ok();
        }

        throw new Exception("Failed to Delete User");
    }
}
