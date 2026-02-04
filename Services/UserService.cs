using asset_monitoring.Data;
using MySqlConnector;
using System.Threading.Tasks;

namespace asset_monitoring.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _db;

        public UserService(ApplicationDbContext db)
        {
            _db = db;
        }

        //    public async Task<int> UpdateUserMasterAsync(
        //        int userId,
        //        string name,
        //        string userType,
        //        string mobileNumber,
        //        string? password,
        //        bool isActive)
        //    {
        //        await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
        //        await conn.OpenAsync();

        //        await using var cmd = new MySqlCommand("sp_update_user_master", conn)
        //        {
        //            CommandType = System.Data.CommandType.StoredProcedure
        //        };

        //        cmd.Parameters.AddWithValue("p_user_id", userId);
        //        cmd.Parameters.AddWithValue("p_name", name);
        //        cmd.Parameters.AddWithValue("p_user_type", userType);
        //        cmd.Parameters.AddWithValue("p_mobile_number", mobileNumber);
        //        cmd.Parameters.AddWithValue("p_password", string.IsNullOrEmpty(password) ? DBNull.Value : password);
        //        cmd.Parameters.AddWithValue("p_is_active", isActive ? 1 : 0);

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        if (await reader.ReadAsync())
        //        {
        //            return reader.GetInt32("rows_affected");
        //        }
        //        return 0;
        //    }
        //}
    }
}