﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Configuration;

namespace IRCbot
{
    public static class DbWorker
    {
        private static string ConnectionString = ConfigurationManager.AppSettings["mysql-cstring"];

        public static MySqlDataReader ExecuteReader(string text)
        {
            return MySqlHelper.ExecuteReader(ConnectionString, text);
        }

        public static MySqlDataReader ExecuteReader(string text, MySqlParameter[] parameters)
        {
            return MySqlHelper.ExecuteReader(ConnectionString, text, parameters);
        }

        public static int ExecuteNonQuery(string text)
        {
            return ExecuteNonQuery(text, null);
        }

        public static int ExecuteNonQuery(string text, MySqlParameter[] parameters)
        {
            return MySqlHelper.ExecuteNonQuery(ConnectionString, text, parameters);
        }
    }
}
