﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ILyncH;
using LyncHEntity;
using LyncHUtil;
using Community.CsharpSqlite.SQLiteClient;
using System.IO;
using System.Diagnostics;
using log4net;
using System.Reflection;

namespace MessageStoreImpl
{
    public class SQLite : IMessageStore
    {
        private string DATABASE_NAME = string.Empty;
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static object locker = new object();

        public SQLite(string database = null)
        {
            string appStartPath = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            DATABASE_NAME = string.Format("{0}", Path.Combine(appStartPath, database));

            LoadDatabaseToDisk(DATABASE_NAME);

            DATABASE_NAME = string.Format("Version=3,uri=file:{0}", DATABASE_NAME);
        }

        private int pageSize = 100;
        public int PageSize
        {
            get { return pageSize; }
            set { pageSize = value; }
        }

        private bool LoadDatabaseToDisk(string database)
        {
            if (!File.Exists(database))
            {
                try
                {
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("MessageStoreImpl" + ".OCH.db"))
                    {
                        if (stream == null)
                            throw new Exception("OCH.db doesn't exists");

                        byte[] bytes = new byte[stream.Length];
                        stream.Position = 0;
                        stream.Read(bytes, 0, (int)stream.Length);
                        
                        File.WriteAllBytes(database, bytes);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return false;
        }

        public void SaveMessage(DateTime beginTime, string messageBody, string[] contacts)
        {
            foreach (var c in contacts)
            {
                Contact contact = SaveContact(c, beginTime);
                SaveContactCoversationDailyDate(contact, beginTime);
                SaveMessage(contact, messageBody, beginTime, false);
            }
        }

        private Contact GetContactByName(string ContactName)
        {
            Contact c = null;

            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format("select * from Contact where ContactName = '{0}'", ContactName.ToLower());

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    c = new Contact();

                                    while (reader.Read())
                                    {
                                        c.Id = int.Parse(reader["Id"].ToString());
                                        c.ContactName = reader["ContactName"].ToString();
                                        c.FriendlyName = reader["FriendlyName"] == null ? string.Empty : reader["FriendlyName"].ToString();
                                        c.LastConversationTime = DateTime.Parse(reader["LastConversationTime"].ToString());
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return c;
        }

        private bool NewContact(string ContactName, DateTime date)
        {
            int result = 0;

            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"insert into Contact (ContactName, LastConversationTime)
select '{0}', '{1}'
where not exists(select 1 from Contact where ContactName ='{0}')", ContactName.ToLower(), DateTime2String(date));

                            result = command.ExecuteNonQuery();

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
                result = -1;
            }

            return result != 0;
        }

        private bool UpdateContact(string ContactName, DateTime date)
        {
            int result = 0;

            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"update Contact set LastConversationTime = '{1}' , IsDeleted = 'false' where ContactName = '{0}'", ContactName.ToLower(), DateTime2String(date));

                            result = command.ExecuteNonQuery();

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
                result = -1;
            }

            return result != 0;
        }

        private Contact SaveContact(string ContactName, DateTime date)
        {
            bool isNewContract = NewContact(ContactName, date);

            if (!isNewContract)
            {
                bool updated = UpdateContact(ContactName, date);
            }

            Contact contact = GetContactByName(ContactName);

            return contact;
        }

        private void SaveContactCoversationDailyDate(Contact contact, DateTime date)
        {
            if (contact != null)
            {
                try
                {
                    lock (locker)
                    {
                        using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                        {
                            connection.Open();

                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = string.Format(@"insert into ContactMessageDate(ContactId, MessageDate)  select '{0}', '{1}'
where not exists(select 1 from ContactMessageDate where ContactId ='{0}' and MessageDate = '{1}')", contact.Id, DateTime2String(date.Date));

                                int result = command.ExecuteNonQuery();

                                command.Dispose();
                            }

                            connection.Close();
                            connection.Dispose();
                        }
                    }
                }
                catch (SqliteException exp)
                {
                    logger.Error(exp);
                }
            }
        }

        private bool NewMessage(Contact contact,
            string messageBody,
            DateTime dtBeginTime,
            bool isCompressed)
        {
            int result = 0;
            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
		insert into Message (ContactId, MessageTime, MessageText, IsCompressed)
		select '{0}', '{1}', '{2}', '{3}'
        where not exists(select 1 from Message where ContactId ='{0}' and MessageTime = '{1}')"
                                , contact.Id, DateTime2String(dtBeginTime), messageBody, isCompressed);

                            result = command.ExecuteNonQuery();

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
                result = -1;
            }

            return result != 0;
        }

        private bool UpdateMessage(Contact contact,
            string messageBody,
            DateTime dtBeginTime,
            bool isCompressed)
        {
            int result = 0;

            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
		update Message 
		   set MessageText = '{2}',
		       IsCompressed = '{3}',
               IsDeleted = 'false'
		 where ContactId = '{0}'
		   and MessageTime = '{1}'"
                                , contact.Id, DateTime2String(dtBeginTime), messageBody, isCompressed);

                            result = command.ExecuteNonQuery();

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
                result = -1;
            }

            return result != 0;
        }

        private void SaveMessage(
            Contact contact,
            string messageBody,
            DateTime dtBeginTime,
            bool isCompressed)
        {
            messageBody = MessageFormatter.EncodeToBase64(messageBody);
            
            bool isNewMessage = NewMessage(contact, messageBody, dtBeginTime, isCompressed);

            if (!isNewMessage)
            {
                bool updated = UpdateMessage(contact, messageBody, dtBeginTime, isCompressed);
            }
        }

        public List<Contact> GetContactList()
        {
            List<Contact> list = new List<Contact>();

            try
            {
                lock (locker)
                {
                    using (SqliteConnection connection = new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"SELECT Id, ContactName, FriendlyName, LastConversationTime
	FROM Contact
    where IsDeleted = 'false'
	order by ContactName";
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        Contact c = new Contact();
                                        c.Id = int.Parse(reader["Id"].ToString());
                                        c.ContactName = reader["ContactName"].ToString();
                                        c.FriendlyName = reader["FriendlyName"] == null ? string.Empty : reader["FriendlyName"].ToString();
                                        c.LastConversationTime = DateTime.Parse(reader["LastConversationTime"].ToString());

                                        list.Add(c);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }

        public void GetQueryMessageList(DateTime dtStart, DateTime dtEnd, string keyword, List<Contact> contactLit, List<LyncMessage> messageList)
        {
            List<Contact> allContacts = GetContactList();

            if (allContacts == null)
            {
                return;
            }

            foreach (Contact contact in allContacts)
            {
                var list = GetMessage(contact, dtStart, dtEnd);

                if (list != null && list.Count > 0)
                {
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        foreach (var message in list)
                        {
                            string plainText = message.MessageText;

                            if (plainText.ToUpper().IndexOf(keyword.ToUpper()) != -1)
                            {
                                messageList.Add(message);

                                if (!contactLit.Contains(contact))
                                {
                                    contactLit.Add(contact);
                                }
                            }
                        }
                    }
                    else
                    {
                        messageList.AddRange(list);
                        contactLit.Add(contact);
                    }
                }
            }
        }

        private List<DateTime> GetCoversationDateListLast(Contact contact)
        {
            List<DateTime> list = new List<DateTime>();

            try
            {
                lock (locker)
                {
                    using (var connection =
                                new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
                            select MessageDate 
                            from (
	                            select * 
		                          from ContactMessageDate
		                         where ContactId = '{0}' and ContainsMessage ='true') 
                                 order by strftime('%s',MessageDate) desc
                            limit '{1}'",
                                contact.Id,
                                pageSize);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        DateTime messageDate = DateTime.Parse(reader["MessageDate"].ToString());
                                        list.Insert(0, messageDate);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }

        private List<DateTime> GetCoversationDateListFirst(Contact contact)
        {
            List<DateTime> list = new List<DateTime>();

            try
            {
                lock (locker)
                {
                    using (var connection =
                                new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
                            select MessageDate 
                            from (
	                            select * 
		                          from ContactMessageDate
		                         where ContactId = '{0}' and ContainsMessage ='true') 
                                 order by strftime('%s',MessageDate) asc
                            limit '{1}'",
                                contact.Id,
                                pageSize);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        DateTime messageDate = DateTime.Parse(reader["MessageDate"].ToString());
                                        list.Add(messageDate);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }


        private List<DateTime> GetCoversationDateListForward(Contact contact, DateTime searchDate)
        {
            List<DateTime> list = new List<DateTime>();

            try
            {
                lock (locker)
                {
                    using (var connection =
                                new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
                            select MessageDate 
                            from (
	                            select * 
		                          from ContactMessageDate
		                         where ContactId = '{0}' and ContainsMessage ='true'
                                   and MessageDate > '{1}'
                                 order by strftime('%s', MessageDate) asc) 
                            limit '{2}'",
                                contact.Id,
                                DateTime2String(searchDate),
                                pageSize);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        DateTime messageDate = DateTime.Parse(reader["MessageDate"].ToString());
                                        list.Add(messageDate);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }

        private List<DateTime> GetCoversationDateListBackward(Contact contract, DateTime searchDate)
        {
            List<DateTime> list = new List<DateTime>();

            try
            {
                lock (locker)
                {
                    using (var connection =
                                new SqliteConnection(DATABASE_NAME))
                    {
                        connection.Open();

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = string.Format(@"
                            select MessageDate 
                            from (
	                            select * 
		                          from ContactMessageDate
		                         where ContactId = '{0}' and ContainsMessage ='true'
                                   and MessageDate < '{1}'
                                 order by strftime('%s',MessageDate) desc) 
                            limit '{2}'",
                                contract.Id,
                                DateTime2String(searchDate),
                                pageSize);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        DateTime messageDate = DateTime.Parse(reader["MessageDate"].ToString());
                                        list.Insert(0, messageDate);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }

        public List<DateTime> GetCoversationDateList(Contact contact, DateTime searchDate, SearchDirection direction = SearchDirection.None)
        {
            List<DateTime> list = new List<DateTime>();

            if ((searchDate == DateTime.MaxValue
                || searchDate == DateTime.MinValue) &&
                        (direction == SearchDirection.Forward ||
                        direction == SearchDirection.Backward))
            {
                return list;
            }

            if (direction == SearchDirection.None
                        || direction == SearchDirection.Last)
            {
                list = GetCoversationDateListLast(contact);
            }
            else if (direction == SearchDirection.First)
            {
                list = GetCoversationDateListFirst(contact);
            }
            else if (direction == SearchDirection.Forward)
            {
                list = GetCoversationDateListForward(contact, searchDate);
            }
            else if (direction == SearchDirection.Backward)
            {
                list = GetCoversationDateListBackward(contact, searchDate);
            }

            return list;
        }

        public List<LyncHEntity.LyncMessage> GetMessage(Contact contact, DateTime dtStart, DateTime dtEnd)
        {
            List<LyncMessage> list = new List<LyncMessage>();

            try
            {
                lock (locker)
                {
                    using (var connection =
                            new SqliteConnection(DATABASE_NAME))
                    {
                        using (var command = connection.CreateCommand())
                        {
                            connection.Open();

                            command.CommandText = string.Format(@"SELECT *
	  from Message
	 where ContactId = '{0}'
	   and MessageTime >= '{1}'
	   and MessageTime < '{2}'
     order by strftime('%s',MessageTime)",
                                    contact.Id,
                                    DateTime2String(dtStart == DateTime.MinValue ?
                                new DateTime(1970, 1, 1) : dtStart),
                                DateTime2String(dtEnd));

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        LyncMessage message = new LyncMessage();
                                        message.ContactId = int.Parse(reader["ContactId"].ToString());
                                        message.MessageText = MessageFormatter.DecodeFromBase64(reader["MessageText"].ToString());
                                        message.MessageTime = DateTime.Parse(reader["MessageTime"].ToString());
                                        message.IsCompressed = bool.Parse(reader["IsCompressed"].ToString());

                                        list.Add(message);
                                    }
                                }

                                reader.Close();
                                reader.Dispose();
                            }

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return list;
        }

        public int GetTotalDailyMessageCount(Contact contract)
        {
            int count = -1;

            try
            {
                lock (locker)
                {
                    using (var connection =
                            new SqliteConnection(DATABASE_NAME))
                    {
                        using (var command = connection.CreateCommand())
                        {
                            connection.Open();

                            command.CommandText = @"select count(*) as MessageCount
	  from ContactMessageDate
	 where ContactId  = @ContactId";

                            command.Parameters.Add(new SqliteParameter
                            {
                                ParameterName = "@ContactId",
                                Value = contract.Id
                            });

                            count = int.Parse(command.ExecuteScalar().ToString());

                            command.Dispose();
                        }

                        connection.Close();
                        connection.Dispose();
                    }
                }
            }
            catch (SqliteException exp)
            {
                logger.Error(exp);
            }

            return count;
        }

        private string DateTime2String(DateTime datetime)
        {
            return datetime.ToString("yyyy-MM-dd HH:mm:ss.ffff");
        }

    }
}
