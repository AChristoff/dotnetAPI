using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace DotnetAPI.Data
{
    class DataContextDapper
    {
        private readonly IConfiguration _config;
        public DataContextDapper(IConfiguration config)
        {
            _config = config;
        }

        public IEnumerable<T> LoadData<T>(string sql, object? parameters = null)
        {
            using IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            return dbConnection.Query<T>(sql, parameters);
        }

        public T LoadDataSingle<T>(string sql, object? parameters = null)
        {
            using IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            return dbConnection.QuerySingle<T>(sql, parameters);
        }

        public bool ExecuteSql(string sql, DynamicParameters? parameters = null)
        {
            using IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            return dbConnection.Execute(sql, parameters) > 0;
        }


        public bool ExecuteSqlWithParameters(string sql, IEnumerable<SqlParameter> parameters)
        {
            try
            {
                using SqlConnection dbConnection = new(_config.GetConnectionString("DefaultConnection"));
                using SqlCommand commandWithParams = new(sql, dbConnection);

                if (parameters != null)
                {
                    foreach (SqlParameter parameter in parameters)
                    {
                        commandWithParams.Parameters.Add(parameter);
                    }
                }

                dbConnection.Open();
                int rowsAffected = commandWithParams.ExecuteNonQuery();

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        public int ExecuteSqlWithRowCount(string sql)
        {
            IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            return dbConnection.Execute(sql);
        }
    }
}