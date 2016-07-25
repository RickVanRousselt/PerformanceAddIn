using System;
using System.Configuration;
using StackExchange.Redis;

namespace PerformanceAddInWeb
{
    public class RedisHelper
    {
        static RedisHelper()
        {
            lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                return ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings.Get("RedisConnString"));
            });
        }

        private static Lazy<ConnectionMultiplexer> lazyConnection;

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }

    }
}