using System.Collections.Generic;

using System.Data.SqlServerCe;
using System.IO;

namespace WPSort
{
    class DBLogs
    {
        private string ConnectionString = "";

        public DBLogs(string CStr)
        {
            ConnectionString = CStr;
            NewDB();
        }

        public void Add(string DocumentNumber, string Barcode)
        {
            if (ConnectionString == "") return;

            SqlCeConnection connect = new SqlCeConnection(ConnectionString);
            connect.Open();
            SqlCeCommand command = new SqlCeCommand("SELECT 1 FROM MainLog WHERE DocumentNumber = @DocN AND Barcode = @Barcode", connect);
            command.Parameters.AddWithValue("@DocN", DocumentNumber);
            command.Parameters.AddWithValue("@Barcode", Barcode);
            command.Prepare();
            if (command.ExecuteScalar() == null)
            {
                command.CommandText = "INSERT INTO MainLog (DocumentNumber, Barcode) VALUES (@DocN, @Barcode)";
                command.ExecuteNonQuery();
            }
            connect.Close();
        }

        public void Move(string DocumentNumber, string Barcode)
        {
            if (ConnectionString == "") return;

            SqlCeConnection connect = new SqlCeConnection(ConnectionString);
            connect.Open();
            SqlCeCommand command = new SqlCeCommand("UPDATE MainLog SET DocumentNumber = @DocN WHERE Barcode = @Barcode", connect);
            command.Parameters.AddWithValue("@DocN", DocumentNumber);
            command.Parameters.AddWithValue("@Barcode", Barcode);
            command.Prepare();
            command.ExecuteNonQuery();
            connect.Close();
        }

        public List<string> GetBarcode(string DocumentNumber)
        {
            List<string> list = new List<string>();

            if (ConnectionString == "") return list;

            SqlCeConnection connect = new SqlCeConnection(ConnectionString);
            connect.Open();

            SqlCeCommand command = new SqlCeCommand("SELECT Barcode FROM MainLog WHERE DocumentNumber = @DocN", connect);
            command.Parameters.AddWithValue("@DocN", DocumentNumber);
            command.Prepare();
            SqlCeDataReader reader = command.ExecuteReader();

            while (reader.Read())
                list.Add((string)reader["Barcode"]);

            connect.Close();
            return list;
        }

        private void NewDB()
        {
            if (ConnectionString == "") return;

            SqlCeConnection connect = new SqlCeConnection(ConnectionString);

            if (!File.Exists(connect.Database))
            {
                SqlCeEngine engine = new SqlCeEngine(ConnectionString);
                engine.CreateDatabase();
                engine.Dispose();
            }

            connect.Open();

            SqlCeCommand command = connect.CreateCommand();

            command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MainLog'";

            if (command.ExecuteScalar() == null)
            {
                command.CommandText = "CREATE TABLE MainLog ( DocumentNumber nvarchar(50) not null, Barcode nvarchar(50) not null);";
                command.ExecuteNonQuery();
                command.CommandText = "CREATE INDEX indDocNum ON MainLog (DocumentNumber);";
                command.ExecuteNonQuery();
            }
            connect.Close();
        }

    }
}
