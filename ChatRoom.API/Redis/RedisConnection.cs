using ChatRoom.API.ReusableClass;
using StackExchange.Redis;
using System;

namespace ChatRoom.API.Redis
{
    public class RedisConnection
    {
        private readonly IConfiguration _configuration;
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisConnection(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionMultiplexer = ConnectionMultiplexer.Connect(_configuration.GetConnectionString("Redis")!);
        }

        public IDatabase GetChatRoomDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:ChatRoom"]!));
        }

        public IDatabase GetFamilyDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Family"]!));
        }

        public IDatabase GetPantryDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Pantry"]!));
        }

        public IDatabase GetTreeholeDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Treehole"]!));
        }

        public IDatabase GetNijigenDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Nijigen"]!));
        }

        public IDatabase GetPartnerCornerDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:PartnerCorner"]!));
        }

        public IDatabase GetBoxingGymDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:BoxingGym"]!));
        }

        public IDatabase GetCircusDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Circus"]!));
        }

        public IDatabase GetConfessionalDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Confessional"]!));
        }

        public IDatabase GetChatRequestDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:ChatRequest"]!));
        }

        public IDatabase? GetDatabaseByChatRoomName(string chatRoomName) 
        {
            switch (chatRoomName)
            {
                case "Family":
                    {
                        return GetFamilyDatabase();
                    }
                case "Pantry":
                    {
                        return GetPantryDatabase();
                    }
                case "Treehole":
                    {
                        return GetTreeholeDatabase();
                    }
                case "Nijigen":
                    {
                        return GetNijigenDatabase();
                    }
                case "PartnerCorner":
                    {
                        return GetPartnerCornerDatabase();
                    }
                case "BoxingGym":
                    {
                        return GetBoxingGymDatabase();
                    }
                case "Circus":
                    {
                        return GetCircusDatabase();
                    }
                case "Confessional":
                    {
                        return GetConfessionalDatabase();
                    }
                default:
                    {
                        return null;
                    }
            }
        }
    }
}
