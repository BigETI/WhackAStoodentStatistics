using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using WhackAStoodentStatistics.Data;

namespace WhackAStoodentStatistics
{
    internal class Program
    {
        private static readonly string configFilePath = "./config.json";

        private static MySqlConnection mysqlConnection;

        public string Host { get; private set; } = string.Empty;

        public string Username { get; private set; } = string.Empty;

        public string Password { get; private set; } = string.Empty;

        public ushort Port { get; private set; }

        public string Database { get; private set; } = string.Empty;

        public IConfiguration Configuration { get; }

        private static void Main()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    ConfigurationData configuration_data = null;
                    using (FileStream file_stream = File.OpenRead(configFilePath))
                    {
                        using StreamReader file_stream_reader = new StreamReader(file_stream);
                        configuration_data = JsonConvert.DeserializeObject<ConfigurationData>(file_stream_reader.ReadToEnd());
                    }
                    if (Protection.IsValid(configuration_data))
                    {
                        using MySqlConnection mysql_connection = new MySqlConnection($"Server={ configuration_data.Host };Port={ configuration_data.Port };User ID={ configuration_data.Username };Password={ configuration_data.Password };Database={ configuration_data.Database }");
                        mysql_connection.Open();
                        mysqlConnection = mysql_connection;
                        WebHost.CreateDefaultBuilder().UseUrls(configuration_data.ListeningTo).Configure((applicationBuilder) => applicationBuilder.Run(RunEvent)).Build().Run();
                        mysql_connection.Close();
                    }
                    else
                    {
                        Console.Error.WriteLine($"Failed to parse file \"{ configFilePath }\". Please check this file for errors.");
                    }
                }
                else
                {
                    using (FileStream file_stream = File.OpenWrite(configFilePath))
                    {
                        using StreamWriter file_stream_writer = new StreamWriter(file_stream);
                        file_stream_writer.Write(JsonConvert.SerializeObject(new ConfigurationData()));
                    }
                    Console.WriteLine($"First time creating \"{ configFilePath }\". Please enter your credentials in that file to connect to continue.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                mysqlConnection = null;
            }
        }

        private static Task RunEvent(HttpContext httpContext)
        {
            object message = null;
            switch (httpContext.Request.Path.Value.EndsWith('/') ? httpContext.Request.Path.Value : $"{ httpContext.Request.Path.Value }/")
            {
                case "/v1/":
                case "/v1/history/":
                    message = AssertDatabaseIsConnected
                    (
                        httpContext,
                        () =>
                        {
                            object ret = null;
                            switch (httpContext.Request.Method)
                            {
                                // List match history
                                case "GET":
                                    if (httpContext.Request.Query.ContainsKey("userid"))
                                    {
                                        ulong count = (httpContext.Request.Query.ContainsKey("count") && ulong.TryParse(httpContext.Request.Query["count"], out ulong current_count)) ? current_count : 50UL;
                                        List<MatchHistoryEntryData> match_history = new List<MatchHistoryEntryData>();
                                        if (count > 0UL)
                                        {
                                            using MySqlCommand command = new MySqlCommand
                                            (
                                                @"(
	SELECT
		`sessions`.`datetime` AS `datetime`,
		`sessions`.`sessionid` AS `sessionid`,
		`sessions`.`useronescore` AS `myscore`,
		`sessions`.`useronerole` AS `myrole`,
		`sessions`.`useronename` AS `myname`,
		`sessions`.`usertwoscore` AS `opponentscore`,
		`sessions`.`usertworole` AS `opponentrole`,
		`sessions`.`usertwoname` AS `opponentname`
	FROM `users`
	INNER JOIN `sessions` ON `users`.`id`=`sessions`.`oneid`
	WHERE `users`.`userid`=@userID
)
UNION
(
	SELECT
		`sessions`.`datetime` AS `datetime`,
		`sessions`.`sessionid` AS `sessionid`,
		`sessions`.`usertwoscore` AS `myscore`,
		`sessions`.`usertworole` AS `myrole`,
		`sessions`.`usertwoname` AS `myname`,
		`sessions`.`useronescore` AS `opponentscore`,
		`sessions`.`useronerole` AS `opponentrole`,
		`sessions`.`useronename` AS `opponentname`
	FROM `users`
	INNER JOIN `sessions` ON `users`.`id`=`sessions`.`twoid`
	WHERE `users`.`userid`=@userID
)
ORDER BY `datetime`
LIMIT @count;",
                                                mysqlConnection
                                            );
                                            command.Parameters.AddWithValue("@userID", httpContext.Request.Query["userid"].ToString());
                                            command.Parameters.AddWithValue("@count", count);
                                            using MySqlDataReader reader = command.ExecuteReader();
                                            while (reader.Read())
                                            {
                                                if
                                                (
                                                    Enum.TryParse(reader.GetFieldValue<string>("myrole"), out EPlayerRole my_role) &&
                                                    Enum.TryParse(reader.GetFieldValue<string>("opponentrole"), out EPlayerRole opponent_role)
                                                )
                                                {
                                                    match_history.Add
                                                    (
                                                        new MatchHistoryEntryData
                                                        (
                                                            reader.GetFieldValue<DateTime>("datetime"),
                                                            reader.GetFieldValue<Guid>("sessionid"),
                                                            reader.GetFieldValue<long>("myscore"),
                                                            my_role,
                                                            reader.GetFieldValue<string>("myname"),
                                                            reader.GetFieldValue<long>("opponentscore"),
                                                            opponent_role,
                                                            reader.GetFieldValue<string>("opponentname")
                                                        )
                                                    );
                                                }
                                            }
                                        }
                                        ret = new { matchData = match_history };
                                    }
                                    else
                                    {
                                        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                        ret = new ErrorData("Missing user ID parameter.");
                                    }
                                    break;

                                // Post match history entry
                                case "POST":
                                    {
                                        using StreamReader stream_reader = new StreamReader(httpContext.Request.Body);
                                        PostMatchData post_match_data = JsonConvert.DeserializeObject<PostMatchData>(stream_reader.ReadToEnd());
                                        if (Protection.IsValid(post_match_data))
                                        {
                                            long my_id = 0L;
                                            using (MySqlCommand check_user_command = new MySqlCommand("SELECT `id` FROM `users` WHERE `userid`=@userID LIMIT 1", mysqlConnection))
                                            {
                                                check_user_command.Parameters.AddWithValue("@userID", post_match_data.UserID);
                                                using MySqlDataReader check_user_reader = check_user_command.ExecuteReader();
                                                if (check_user_reader.Read())
                                                {
                                                    my_id = check_user_reader.GetFieldValue<long>("id");
                                                }
                                                else
                                                {
                                                    check_user_reader.Close();
                                                    using MySqlCommand insert_user_command = new MySqlCommand("INSERT INTO `users` (`userid`) VALUES (@userID)", mysqlConnection);
                                                    insert_user_command.Parameters.AddWithValue("@userID", post_match_data.UserID);
                                                    insert_user_command.ExecuteNonQuery();
                                                    using MySqlCommand get_user_id_command = new MySqlCommand("SELECT `id` FROM `users` WHERE `userid`=@userID LIMIT 1", mysqlConnection);
                                                    get_user_id_command.Parameters.AddWithValue("@userID", post_match_data.UserID);
                                                    using MySqlDataReader get_user_id_reader = get_user_id_command.ExecuteReader();
                                                    if (get_user_id_reader.Read())
                                                    {
                                                        my_id = get_user_id_reader.GetFieldValue<long>("id");
                                                    }
                                                }
                                            }
                                            if (my_id > 0L)
                                            {
                                                using MySqlCommand check_session_command = new MySqlCommand("SELECT `id` FROM `sessions` WHERE `sessionid`=@sessionID LIMIT 1", mysqlConnection);
                                                check_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                using MySqlDataReader check_session_reader = check_session_command.ExecuteReader();
                                                if (check_session_reader.HasRows)
                                                {
                                                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                                    ret = new ErrorData("Match is already posted.");
                                                }
                                                else
                                                {
                                                    check_session_reader.Close();
                                                    using MySqlCommand check_staged_session_command = new MySqlCommand("SELECT `id` FROM `pendingsessions` WHERE `myid`=@myID AND `sessionid`=@sessionID LIMIT 1", mysqlConnection);
                                                    check_staged_session_command.Parameters.AddWithValue("@myID", my_id);
                                                    check_staged_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                    using MySqlDataReader check_staged_session_reader = check_staged_session_command.ExecuteReader();
                                                    if (check_staged_session_reader.HasRows)
                                                    {
                                                        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                                        ret = new ErrorData("Match is already posted.");
                                                    }
                                                    else
                                                    {
                                                        check_staged_session_reader.Close();
                                                        using MySqlCommand get_opponent_staged_session_command = new MySqlCommand("SELECT * FROM `pendingsessions` WHERE `sessionid`=@sessionID LIMIT 1", mysqlConnection);
                                                        get_opponent_staged_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                        using MySqlDataReader get_opponent_staged_session_reader = get_opponent_staged_session_command.ExecuteReader();
                                                        if (get_opponent_staged_session_reader.Read())
                                                        {
                                                            if
                                                            (
                                                                Enum.TryParse(get_opponent_staged_session_reader.GetFieldValue<string>("myrole"), out EPlayerRole opponent_my_role) &&
                                                                (opponent_my_role != EPlayerRole.Invalid) &&
                                                                Enum.TryParse(get_opponent_staged_session_reader.GetFieldValue<string>("opponentrole"), out EPlayerRole opponent_opponent_role) &&
                                                                (opponent_opponent_role != EPlayerRole.Invalid)
                                                            )
                                                            {
                                                                long opponent_id = get_opponent_staged_session_reader.GetFieldValue<long>("myid");
                                                                long opponent_my_score = get_opponent_staged_session_reader.GetFieldValue<long>("myscore");
                                                                string opponent_my_name = get_opponent_staged_session_reader.GetFieldValue<string>("myname");
                                                                long opponent_opponent_score = get_opponent_staged_session_reader.GetFieldValue<long>("opponentscore");
                                                                string opponent_opponent_name = get_opponent_staged_session_reader.GetFieldValue<string>("opponentname");
                                                                get_opponent_staged_session_reader.Close();
                                                                if
                                                                (
                                                                    (post_match_data.MatchData.OpponentScore == opponent_my_score) &&
                                                                    (post_match_data.MatchData.OpponentName == opponent_my_name) &&
                                                                    (post_match_data.MatchData.OpponentRole == opponent_my_role) &&
                                                                    (post_match_data.MatchData.YourScore == opponent_opponent_score) &&
                                                                    (post_match_data.MatchData.YourName == opponent_opponent_name) &&
                                                                    (post_match_data.MatchData.YourRole == opponent_opponent_role)
                                                                )
                                                                {
                                                                    using MySqlCommand remove_opponent_staged_session_command = new MySqlCommand("DELETE FROM `pendingsessions` WHERE `sessionID`=@sessionID", mysqlConnection);
                                                                    remove_opponent_staged_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                                    remove_opponent_staged_session_command.ExecuteNonQuery();
                                                                    using MySqlCommand insert_session_command = new MySqlCommand("INSERT INTO `sessions` (`sessionid`, `useronescore`, `useronerole`, `useronename`, `usertwoscore`, `usertworole`, `usertwoname`, `oneid`, `twoid`) VALUES (@sessionID, @userOneScore, @userOneRole, @userOneName, @userTwoScore, @userTwoRole, @userTwoName, @oneID, @twoID)", mysqlConnection);
                                                                    insert_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                                    insert_session_command.Parameters.AddWithValue("@userOneScore", post_match_data.MatchData.OpponentScore);
                                                                    insert_session_command.Parameters.AddWithValue("@userOneRole", post_match_data.MatchData.OpponentRole.ToString());
                                                                    insert_session_command.Parameters.AddWithValue("@userOneName", post_match_data.MatchData.OpponentName);
                                                                    insert_session_command.Parameters.AddWithValue("@userTwoScore", post_match_data.MatchData.YourScore);
                                                                    insert_session_command.Parameters.AddWithValue("@userTwoRole", post_match_data.MatchData.YourRole.ToString());
                                                                    insert_session_command.Parameters.AddWithValue("@userTwoName", post_match_data.MatchData.YourName);
                                                                    insert_session_command.Parameters.AddWithValue("@oneID", opponent_id);
                                                                    insert_session_command.Parameters.AddWithValue("@twoID", my_id);
                                                                    insert_session_command.ExecuteNonQuery();
                                                                    ret = true;
                                                                }
                                                                else
                                                                {
                                                                    using MySqlCommand remove_opponent_staged_session_command = new MySqlCommand("DELETE FROM `pendingsessions` WHERE `sessionID`=@sessionID", mysqlConnection);
                                                                    remove_opponent_staged_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                                    remove_opponent_staged_session_command.ExecuteNonQuery();
                                                                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                                                    ret = new ErrorData("Match discrepency detected.");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                                                ret = new ErrorData("Failed to parse staged session entry from database.");
                                                            }
                                                        }
                                                        else
                                                        {
                                                            get_opponent_staged_session_reader.Close();
                                                            using MySqlCommand insert_staged_session_command = new MySqlCommand("INSERT INTO `pendingsessions` (`sessionid`, `myscore`, `myrole`, `myname`, `opponentscore`, `opponentrole`, `opponentname`, `myid`) VALUES (@sessionID, @myScore, @myRole, @myName, @opponentScore, @opponentRole, @opponentName, @myID)", mysqlConnection);
                                                            insert_staged_session_command.Parameters.AddWithValue("@sessionID", post_match_data.MatchData.SessionID);
                                                            insert_staged_session_command.Parameters.AddWithValue("@myScore", post_match_data.MatchData.YourScore);
                                                            insert_staged_session_command.Parameters.AddWithValue("@myRole", post_match_data.MatchData.YourRole.ToString());
                                                            insert_staged_session_command.Parameters.AddWithValue("@myName", post_match_data.MatchData.YourName);
                                                            insert_staged_session_command.Parameters.AddWithValue("@opponentScore", post_match_data.MatchData.OpponentScore);
                                                            insert_staged_session_command.Parameters.AddWithValue("@opponentRole", post_match_data.MatchData.OpponentRole.ToString());
                                                            insert_staged_session_command.Parameters.AddWithValue("@opponentName", post_match_data.MatchData.OpponentName);
                                                            insert_staged_session_command.Parameters.AddWithValue("@myID", my_id);
                                                            insert_staged_session_command.ExecuteNonQuery();
                                                            ret = true;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                                ret = new ErrorData("Failed to obtain user's database ID.");
                                            }
                                        }
                                        else
                                        {
                                            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                            ret = new ErrorData("Invalid post match data.");
                                        }
                                    }
                                    break;

                                default:
                                    httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                                    ret = new ErrorData($"Unsupported method { httpContext.Request.Method }");
                                    break;
                            }
                            return ret;
                        }
                    );
                    break;
                case "/v1/stats/":
                    message = AssertDatabaseIsConnected
                    (
                        httpContext,
                        () =>
                        {
                            object ret = null;
                            switch (httpContext.Request.Method)
                            {
                                // Get user stats
                                case "GET":
                                    if (httpContext.Request.Query.ContainsKey("userid") && Guid.TryParse(httpContext.Request.Query["userid"].ToString(), out Guid user_id))
                                    {
                                        ulong game_play_count = 0UL;
                                        ulong game_win_count = 0UL;
                                        ulong game_loose_count = 0UL;
                                        ulong game_tie_count = 0UL;
                                        DateTime last_play_date_time = DateTime.Now;
                                        using (MySqlCommand get_play_count_command = new MySqlCommand(@"SELECT
	`sessions`.`datetime` AS `datetime`,
	COUNT(*) AS `playcount`
FROM `users`
INNER JOIN `sessions` ON `users`.`id`=`sessions`.`oneid` OR `users`.`id`=`sessions`.`twoid`
WHERE `users`.`userid`=@userID
ORDER BY `datetime` DESC;", mysqlConnection))
                                        {
                                            get_play_count_command.Parameters.AddWithValue("@userID", user_id);
                                            using MySqlDataReader get_play_count_reader = get_play_count_command.ExecuteReader();
                                            if (get_play_count_reader.Read())
                                            {
                                                last_play_date_time = get_play_count_reader.GetFieldValue<DateTime>("datetime");
                                                game_play_count = get_play_count_reader.GetFieldValue<ulong>("playcount");
                                            }
                                        }
                                        if (game_play_count > 0UL)
                                        {
                                            using (MySqlCommand get_win_count_command = new MySqlCommand(@"SELECT
	COUNT(*) AS `wincount`
FROM `users`
INNER JOIN `sessions` ON `users`.`id`=`sessions`.`oneid` OR `users`.`id`=`sessions`.`twoid`
WHERE (`users`.`userid`=@userID) AND ((`users`.`id`=`sessions`.`oneid` AND `sessions`.`useronescore`>`sessions`.`usertwoscore`) OR (`users`.`id`=`sessions`.`twoid` AND `sessions`.`useronescore`<`sessions`.`usertwoscore`))", mysqlConnection))
                                            {
                                                get_win_count_command.Parameters.AddWithValue("@userID", user_id);
                                                using MySqlDataReader get_win_count_reader = get_win_count_command.ExecuteReader();
                                                if (get_win_count_reader.Read())
                                                {
                                                    game_win_count = get_win_count_reader.GetFieldValue<ulong>("wincount");
                                                }
                                            }
                                            using (MySqlCommand get_loose_count_command = new MySqlCommand(@"SELECT
	COUNT(*) AS `loosecount`
FROM `users`
INNER JOIN `sessions` ON `users`.`id`=`sessions`.`oneid` OR `users`.`id`=`sessions`.`twoid`
WHERE (`users`.`userid`=@userID) AND ((`users`.`id`=`sessions`.`oneid` AND `sessions`.`useronescore`<`sessions`.`usertwoscore`) OR (`users`.`id`=`sessions`.`twoid` AND `sessions`.`useronescore`>`sessions`.`usertwoscore`))", mysqlConnection))
                                            {
                                                get_loose_count_command.Parameters.AddWithValue("@userID", user_id);
                                                using MySqlDataReader get_loose_count_reader = get_loose_count_command.ExecuteReader();
                                                if (get_loose_count_reader.Read())
                                                {
                                                    game_loose_count = get_loose_count_reader.GetFieldValue<ulong>("loosecount");
                                                }
                                            }
                                            using (MySqlCommand get_tie_count_command = new MySqlCommand(@"SELECT
	COUNT(*) AS `tiecount`
FROM `users`
INNER JOIN `sessions` ON `users`.`id`=`sessions`.`oneid` OR `users`.`id`=`sessions`.`twoid`
WHERE (`users`.`userid`=@userID) AND `sessions`.`useronescore`=`sessions`.`usertwoscore`", mysqlConnection))
                                            {
                                                get_tie_count_command.Parameters.AddWithValue("@userID", user_id);
                                                using MySqlDataReader get_tie_count_reader = get_tie_count_command.ExecuteReader();
                                                if (get_tie_count_reader.Read())
                                                {
                                                    game_tie_count = get_tie_count_reader.GetFieldValue<ulong>("tiecount");
                                                }
                                            }
                                        }
                                        ret = new UserStatisticsData(user_id, game_play_count, game_win_count, game_loose_count, game_tie_count, last_play_date_time);
                                    }
                                    else
                                    {
                                        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                                        ret = new ErrorData("Missing user ID parameter.");
                                    }
                                    break;
                                default:
                                    httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                                    ret = new ErrorData($"Unsupported method { httpContext.Request.Method }");
                                    break;
                            }
                            return ret;
                        }
                    );
                    break;
                default:
                    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    message = new ErrorData("No content");
                    break;
            }
            if (message == null)
            {
                message = new ErrorData("Unknown error!");
            }
            httpContext.Response.ContentType = "application/json";
            return (message is bool) ? Task.CompletedTask : httpContext.Response.WriteAsync(JsonConvert.SerializeObject(message));
        }

        private static object AssertDatabaseIsConnected(HttpContext httpContext, DatabaseIsConnectedDelegate onDatabaseIsConnected)
        {
            if (onDatabaseIsConnected == null)
            {
                throw new ArgumentNullException(nameof(onDatabaseIsConnected));
            }
            object ret = null;
            switch (mysqlConnection.State)
            {
                case ConnectionState.Closed:
                    ret = new ErrorData("Database is closed!");
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    break;
                case ConnectionState.Open:
                    ret = onDatabaseIsConnected();
                    break;
                case ConnectionState.Connecting:
                    ret = new ErrorData("Database is still connecting...");
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    break;
                case ConnectionState.Executing:
                    ret = new ErrorData("Database is still executing?");
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    break;
                case ConnectionState.Fetching:
                    ret = new ErrorData("Database is still fetching?");
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    break;
                case ConnectionState.Broken:
                    ret = new ErrorData("Database is broken. :(");
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    break;
            }
            return ret ?? new ErrorData("Unknown error!");
        }
    }
}
