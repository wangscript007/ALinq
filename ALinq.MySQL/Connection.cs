using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MySql.Data.Types;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace ALinq.MySQL
{
    public class Connection : DbConnection
    {
        private readonly DbConnection source;
        private List<DbConnection> otherConnections;

        private string connectionString;

        public Connection(string connectionString)
            : this(new MySql.Data.MySqlClient.MySqlConnection(connectionString))
        {
            this.connectionString = connectionString;
            this.otherConnections = new List<DbConnection>();
        }

        internal Connection(DbConnection source)
        {
            Debug.Assert(source != null);
            this.source = source;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            var sourceTran = source.BeginTransaction(isolationLevel);
            //return new Transaction(sourceTran, this);
            return sourceTran;
        }

        public override void ChangeDatabase(string databaseName)
        {
            source.ChangeDatabase(databaseName);
        }

        public override void Close()
        {
            source.Close();
        }

        public override string ConnectionString
        {
            get
            {
                return source.ConnectionString;
            }
            set
            {
                source.ConnectionString = value;
            }
        }

        protected override DbCommand CreateDbCommand()
        {
            var sourceCommand = source.CreateCommand();
            return new Command(sourceCommand, this);
        }

        public override string DataSource
        {
            get { return source.DataSource; }
        }

        public override string Database
        {
            get { return source.Database; }
        }

        public override void Open()
        {
            source.Open();
        }

        public override string ServerVersion
        {
            get { return source.ServerVersion; }
        }

        public override ConnectionState State
        {
            get { return source.State; }
        }

        public DbConnection Source
        {
            get { return source; }
        }

        public override bool Equals(object obj)
        {
            if (obj is Connection)
                return source == ((Connection)obj).source;
            if (obj is DbConnection)
                return source == obj;
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Source.GetHashCode();
        }

        internal DbDataReader CreateReader(string commandText, DbParameterCollection paramters, CommandBehavior behavior)
        {
            //======================================================
            // 说明：由于一个 MySqlConnection 只能打开一个 reader，
            // 而在查询中一般需要打开多个 reader，因此需要创建多个
            // Connection
            var conn = new MySqlConnection(this.connectionString);
            this.otherConnections.Add(conn);
            var command = conn.CreateCommand();
            command.CommandText = commandText;
            if (paramters != null)
            {
                foreach (var p in paramters)
                    command.Parameters.Add(p);
            }

            conn.Open();
            var reader = command.ExecuteReader(behavior);
            return new DataReader(reader, () =>
            {
                conn.Close();
            });
            //======================================================
        }



    }


    public class Command : DbCommand
    {
        private readonly DbCommand source;
        private Connection connection;

        internal Command(DbCommand source, Connection connection)
        {
            this.source = source;
            this.connection = connection;
        }

        public override void Prepare()
        {
            source.Prepare();
        }

        public override string CommandText
        {
            get
            {
                return source.CommandText;
            }
            set
            {
                var commandText = Regex.Replace(value, @"\[[^\]]+\]", new MatchEvaluator(delegate (Match match)
                {
                    var name = match.Value;
                    Debug.Assert(name.Length > 2);
                    name = name.Substring(1, name.Length - 2);
                    return name;
                }));

                source.CommandText = commandText;
            }
        }

        public override int CommandTimeout
        {
            get { return source.CommandTimeout; }
            set { source.CommandTimeout = value; }
        }

        public override CommandType CommandType
        {
            get { return source.CommandType; }
            set { source.CommandType = value; }
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get { return source.UpdatedRowSource; }
            set { source.UpdatedRowSource = value; }
        }

        protected override DbConnection DbConnection
        {
            get { return connection; }
            set { connection = (Connection)value; }
        }

        protected override DbParameterCollection DbParameterCollection
        {
            get { return source.Parameters; }
        }

        protected override DbTransaction DbTransaction
        {
            get
            {
                return source.Transaction;
            }
            set
            {
                source.Transaction = value;
            }
        }

        public override bool DesignTimeVisible
        {
            get { return source.DesignTimeVisible; }
            set { source.DesignTimeVisible = value; }
        }

        public override void Cancel()
        {
            source.Cancel();
        }

        protected override DbParameter CreateDbParameter()
        {
            return source.CreateParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var reader = this.connection.CreateReader(CommandText, this.Parameters, behavior);
            return reader;
        }

        private DbDataReader CreateNewReader(CommandBehavior behavior)
        {
            var command = this.Connection.CreateCommand();
            var reader = command.ExecuteReader(behavior);
            return reader;
        }

        public override int ExecuteNonQuery()
        {
            return source.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            return source.ExecuteScalar();
        }
    }


    /// <summary>
    /// DbDataReader 的包装类
    /// </summary>
    internal class DataReader : DbDataReader
    {
        private readonly DbDataReader source;
        private Action closeConnection;

        internal DataReader(DbDataReader source, Action closeConnection)
        {
            this.source = source;
            this.closeConnection = closeConnection;
        }

        public DataReader(DbDataReader source)
            : this(source, null)
        {

        }

        public override void Close()
        {
            source.Close();
            if (this.closeConnection != null)
                this.closeConnection();
        }

        public override DataTable GetSchemaTable()
        {
            return source.GetSchemaTable();
        }

        public override bool NextResult()
        {
            return source.NextResult();
        }

        public override bool Read()
        {
            return source.Read();
        }

        public override int Depth
        {
            get
            {
                return source.Depth;
            }
        }

        public override bool IsClosed
        {
            get
            {
                return source.IsClosed;
            }
        }

        public override int RecordsAffected
        {
            get
            {
                return source.RecordsAffected;
            }
        }

        public override int FieldCount
        {
            get
            {
                return source.FieldCount;
            }
        }

        public override object this[int ordinal]
        {
            get
            {
                return source[ordinal];
            }
        }

        public override object this[string name]
        {
            get
            {
                return source[name];
            }
        }

        public override bool HasRows
        {
            get
            {
                return source.HasRows;
            }
        }

        public override bool GetBoolean(int ordinal)
        {
            return Convert.ToBoolean(source.GetValue(ordinal));
        }

        public override byte GetByte(int ordinal)
        {
            return Convert.ToByte(source.GetValue(ordinal));
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            return source.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override char GetChar(int ordinal)
        {
            return source.GetChar(ordinal);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            return source.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override Guid GetGuid(int ordinal)
        {
            return source.GetGuid(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            return Convert.ToInt16(source.GetValue(ordinal));
        }

        public override int GetInt32(int ordinal)
        {
            return Convert.ToInt32(source.GetValue(ordinal));
        }

        public override long GetInt64(int ordinal)
        {
            return Convert.ToInt64(source.GetValue(ordinal));
        }

        public override DateTime GetDateTime(int ordinal)
        {
            var value = source.GetValue(ordinal);
            if (value is MySqlDateTime)
            {
                //if (!((MySqlDateTime)value).IsValidDateTime)
                //    return DateTime.MinValue;
                //else
                return ((MySqlDateTime)value).GetDateTime();
            }
            return Convert.ToDateTime(value);
        }

        public override string GetString(int ordinal)
        {
            return source.GetString(ordinal);
        }

        public override object GetValue(int ordinal)
        {
            return source.GetValue(ordinal);
        }

        public override int GetValues(object[] values)
        {
            return source.GetValues(values);
        }

        public override bool IsDBNull(int ordinal)
        {
            var value = GetValue(ordinal);
            if (value is MySqlDateTime)
            {
                return !((MySqlDateTime)value).IsValidDateTime;
            }

            return DBNull.Value == value;
        }


        public override decimal GetDecimal(int ordinal)
        {
            return Convert.ToDecimal(source.GetValue(ordinal));
        }

        public override double GetDouble(int ordinal)
        {
            return Convert.ToDouble(source.GetValue(ordinal));
        }

        public override float GetFloat(int ordinal)
        {
            return Convert.ToSingle(source.GetValue(ordinal));
        }

        public override string GetName(int ordinal)
        {
            return source.GetName(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            return source.GetOrdinal(name);
        }

        public override string GetDataTypeName(int ordinal)
        {
            return source.GetDataTypeName(ordinal);
        }

        public override Type GetFieldType(int ordinal)
        {
            return source.GetFieldType(ordinal);
        }

        public override IEnumerator GetEnumerator()
        {
            return source.GetEnumerator();
        }
    }
}
