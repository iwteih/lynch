using LyncHEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ILyncH
{
    public interface IMessageStore
    {
        int PageSize { get; set; }

        void SaveMessage(DateTime beginTime, string messageBody, string[] contracts);

        List<Contact> GetContactList();

        void GetQueryMessageList(
            DateTime dtStart, 
            DateTime dtEnd, 
            string keyword, 
            List<Contact> contactLit, 
            List<LyncMessage> messageList);

        List<DateTime> GetCoversationDateList(
            Contact contact, 
            DateTime searchDate, 
            SearchDirection direction = SearchDirection.None);

        List<LyncMessage> GetMessage(Contact contract, DateTime dtStart, DateTime dtEnd);

        int GetTotalDailyMessageCount(Contact contract);
    }
}
