using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AsyncDolls;
using NUnit.Framework;
using IsolationLevel = System.Transactions.IsolationLevel;

namespace RearchitectTowardsAsyncAwait
{
    [TestFixture]
    public class SqlConnectionAsync
    {
        [Test]
        public async Task Demonstrate()
        {
            const string connectionString = @"Data Source=.\SQLEXPRESS;Database=TestDatabase;Integrated Security=true";
            const string queueName = "TestQueue";
            const int numberOfMessages = 100;

            "Creating queue".Output();
            QueueHelper.CreateQueue(connectionString, queueName);
            "Filling queue".Output();
            QueueHelper.FillQueue(connectionString, queueName, numberOfMessages);

            "Kicking it".Output();
            var receives = new ConcurrentDictionary<Task, Task>();
            var stopwatch = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(100);
            var runner = Task.Run(async () =>
            {
                for (var i = 0; i < numberOfMessages; i++)
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);

#pragma warning disable 4014
                    var receive = Receive(connectionString, queueName).ContinueWith(t =>
                    {
                        Task dummy;
                        receives.TryRemove(t, out dummy);
                        semaphore.Release();
                    }, TaskContinuationOptions.ExecuteSynchronously);
                    receives.TryAdd(receive, receive);
#pragma warning restore 4014
                }
            });

            await runner.ConfigureAwait(false);
            await Task.WhenAll(receives.Values.ToArray()).ConfigureAwait(false);
            stopwatch.Stop();

            Console.WriteLine(stopwatch.Elapsed);

            Console.ReadLine();
        }

        static async Task Receive(string connectionString, string queueName)
        {
            using (var tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var promotableSinglePhaseNotification = new Enlistment();
                Transaction.Current.EnlistDurable(Guid.NewGuid(), promotableSinglePhaseNotification, EnlistmentOptions.EnlistDuringPrepareRequired);

                using (var command = new SqlCommand(string.Format(@"WITH message AS (SELECT TOP(1) * FROM [{0}].[{1}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion])
			DELETE FROM message
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress,
			deleted.Recoverable, CASE WHEN deleted.Expires IS NOT NULL THEN DATEDIFF(ms, GETUTCDATE(), deleted.Expires) END, deleted.Headers, deleted.Body;", "dbo", queueName)))
                {
                    command.Connection = connection;

                    using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess).ConfigureAwait(false))
                    {
                        try
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                await MessageRow.Read(reader).ConfigureAwait(false);
                            }
                        }
                        catch (Exception)
                        {
                            Console.Write("!");
                        }
                    }
                }

                tx.Complete();
            }
        }
    }

    class Enlistment : IPromotableSinglePhaseNotification, ISinglePhaseNotification
    {
        public byte[] Promote()
        {
            return new byte[] {1, 2, 3};
        }

        public void Initialize()
        {
        }

        public void SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            singlePhaseEnlistment.Committed();
        }

        public void Rollback(SinglePhaseEnlistment singlePhaseEnlistment)
        {
           singlePhaseEnlistment.Aborted();
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            preparingEnlistment.Prepared();
        }

        public void Commit(System.Transactions.Enlistment enlistment)
        {
            enlistment.Done();
        }

        public void Rollback(System.Transactions.Enlistment enlistment)
        {
            enlistment.Done();
        }

        public void InDoubt(System.Transactions.Enlistment enlistment)
        {
           enlistment.Done();
        }
    }

    class QueueHelper
    {
        public static void CreateQueue(string connectionString, string queueName)
        {
            var ddl = @"IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}]') AND type in (N'U'))
                  BEGIN
                    EXEC sp_getapplock @Resource = '{0}_{1}_lock', @LockMode = 'Exclusive'
                    IF NOT  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[{1}]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [{0}].[{1}](
	                        [Id] [uniqueidentifier] NOT NULL,
	                        [CorrelationId] [varchar](255) NULL,
	                        [ReplyToAddress] [varchar](255) NULL,
	                        [Recoverable] [bit] NOT NULL,
	                        [Expires] [datetime] NULL,
	                        [Headers] [varchar](max) NOT NULL,
	                        [Body] [varbinary](max) NULL,
	                        [RowVersion] [bigint] IDENTITY(1,1) NOT NULL
                        ) ON [PRIMARY];
                        CREATE CLUSTERED INDEX [Index_RowVersion] ON [{0}].[{1}]
                        (
	                        [RowVersion] ASC
                        )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
                        CREATE NONCLUSTERED INDEX [Index_Expires] ON [{0}].[{1}]
                        (
	                        [Expires] ASC
                        )
                        INCLUDE
                        (
                            [Id],
                            [RowVersion]
                        )
                        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
                    END
                    EXEC sp_releaseapplock @Resource = '{0}_{1}_lock'
                  END
                  TRUNCATE TABLE [{0}].[{1}]";

            var commandText = string.Format(ddl, "dbo", queueName);

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand(commandText, connection, transaction))
                    {
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public static void FillQueue(string connectionString, string queueName, int numberOfMessages)
        {
            var random = new Random();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    for (var i = 0; i < numberOfMessages; i++)
                    {
                        using (var command = new SqlCommand(string.Format(SendText, "dbo", queueName), connection, transaction))
                        {
                            var body = new byte[4096];
                            random.NextBytes(body);
                            MessageRow row = MessageRow.From(new Dictionary<string, string>(), body);
                            row.PrepareSendCommand(command);
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        internal const string SendText =
@"INSERT INTO [{0}].[{1}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body])
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,CASE WHEN @TimeToBeReceivedMs IS NOT NULL THEN DATEADD(ms, @TimeToBeReceivedMs, GETUTCDATE()) END,@Headers,@Body)";
    }


    class MessageRow
    {
        MessageRow() { }

        public static async Task<MessageReadResult> Read(SqlDataReader dataReader)
        {
            var row = await ReadRow(dataReader).ConfigureAwait(false);

            var result = row.TryParse();

            return result;
        }


        public static MessageRow From(Dictionary<string, string> headers, byte[] body)
        {
            var row = new MessageRow();

            row.id = Guid.NewGuid();
            row.correlationId = TryGetHeaderValue(headers, "CorrelationId", s => s);
            row.replyToAddress = TryGetHeaderValue(headers, "ReplyToAddress", s => s);
            row.recoverable = true;
            row.timeToBeReceived = TryGetHeaderValue(headers, "TimeToBeReceived", s =>
            {
                TimeSpan timeToBeReceived;
                return TimeSpan.TryParse(s, out timeToBeReceived)
                    ? (int?)timeToBeReceived.TotalMilliseconds
                    : null;
            });
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body;

            return row;
        }

        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Id", SqlDbType.UniqueIdentifier, id);
            AddParameter(command, "CorrelationId", SqlDbType.VarChar, correlationId);
            AddParameter(command, "ReplyToAddress", SqlDbType.VarChar, replyToAddress);
            AddParameter(command, "Recoverable", SqlDbType.Bit, recoverable);
            AddParameter(command, "TimeToBeReceivedMs", SqlDbType.Int, timeToBeReceived);
            AddParameter(command, "Headers", SqlDbType.VarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes ?? bodyStream.ToArray());
        }

        static async Task<MessageRow> ReadRow(SqlDataReader dataReader)
        {
            var row = new MessageRow();

            //HINT: we are assuming that dataReader is sequential. Order or reads is important !
            row.id = await dataReader.GetFieldValueAsync<Guid>(0).ConfigureAwait(false);
            row.correlationId = await GetNullableAsync<string>(dataReader, 1).ConfigureAwait(false);
            row.replyToAddress = await GetNullableAsync<string>(dataReader, 2).ConfigureAwait(false);
            row.recoverable = await dataReader.GetFieldValueAsync<bool>(3).ConfigureAwait(false);
            row.timeToBeReceived = await GetNullableValueAsync<int>(dataReader, 4).ConfigureAwait(false);
            row.headers = await GetHeaders(dataReader, 5).ConfigureAwait(false);
            row.bodyStream = await GetBody(dataReader, 6).ConfigureAwait(false);

            return row;
        }

        MessageReadResult TryParse()
        {
            try
            {
                var parsedHeaders = string.IsNullOrEmpty(headers)
                    ? new Dictionary<string, string>()
                    : DictionarySerializer.DeSerialize(headers);

                var expired = timeToBeReceived.HasValue && timeToBeReceived.Value < 0;

                if (expired)
                {
                    return MessageReadResult.NoMessage;
                }

                return MessageReadResult.Success(new Message(id.ToString(), parsedHeaders, bodyStream));
            }
            catch (Exception)
            {
                return MessageReadResult.Poison(this);
            }
        }

        static T TryGetHeaderValue<T>(Dictionary<string, string> headers, string name, Func<string, T> conversion)
        {
            string text;
            if (!headers.TryGetValue(name, out text))
            {
                return default(T);
            }
            var value = conversion(text);
            return value;
        }

        static async Task<string> GetHeaders(SqlDataReader dataReader, int headersIndex)
        {
            if (await dataReader.IsDBNullAsync(headersIndex).ConfigureAwait(false))
            {
                return null;
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                var headersAsString = await textReader.ReadToEndAsync().ConfigureAwait(false);
                return headersAsString;
            }
        }

        static async Task<MemoryStream> GetBody(SqlDataReader dataReader, int bodyIndex)
        {
            var memoryStream = new MemoryStream();
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }

        static async Task<T> GetNullableAsync<T>(SqlDataReader dataReader, int index) where T : class
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }

            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }

        static async Task<T?> GetNullableValueAsync<T>(SqlDataReader dataReader, int index) where T : struct
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }

            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }

        void AddParameter(SqlCommand command, string name, SqlDbType type, object value)
        {
            command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        }

        Guid id;
        string correlationId;
        string replyToAddress;
        bool recoverable;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;
        MemoryStream bodyStream;
    }

    struct MessageReadResult
    {
        MessageReadResult(Message message, MessageRow poisonMessage)
        {
            Message = message;
            PoisonMessage = poisonMessage;
        }

        public static MessageReadResult NoMessage = new MessageReadResult(null, null);

        public bool IsPoison => PoisonMessage != null;

        public bool Successful => Message != null;

        public Message Message { get; }

        public MessageRow PoisonMessage { get; }

        public static MessageReadResult Poison(MessageRow messageRow)
        {
            return new MessageReadResult(null, messageRow);
        }

        public static MessageReadResult Success(Message message)
        {
            return new MessageReadResult(message, null);
        }
    }

    class Message
    {
        public Message(string transportId, Dictionary<string, string> headers, Stream bodyStream)
        {
            TransportId = transportId;
            BodyStream = bodyStream;
            Headers = headers;
        }

        public string TransportId { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; private set; }
    }

    static class DictionarySerializer
    {
        public static string Serialize(Dictionary<string, string> instance)
        {
            var serializer = BuildSerializer();
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static Dictionary<string, string> DeSerialize(string json)
        {
            var serializer = BuildSerializer();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (Dictionary<string, string>)serializer.ReadObject(stream);
            }
        }

        static DataContractJsonSerializer BuildSerializer()
        {
            var settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };
            return new DataContractJsonSerializer(typeof(Dictionary<string, string>), settings);
        }
    }
}
