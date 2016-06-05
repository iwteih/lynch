using log4net;
using LyncHEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ILyncH
{
    public abstract class MessageProvider
    {
        protected IMessageStore messageStore;

        private static readonly string PRETTY_MESSAGE =@"<DIV style=""FONT-FAMILY: MS Shell Dlg 2; DIRECTION: ltr; COLOR: #000000; FONT-SIZE: 9pt"">{0}</DIV>";
        //0 timespan 1 sender 2 message
        static string PRETTY_MESSAGE_FORMAT = @"<DIV>
<DIV style=""POSITION: relative; PADDING-BOTTOM: 0px; PADDING-LEFT: 3px; WIDTH: 100%; PADDING-RIGHT: 3px; FONT-FAMILY: MS Shell Dlg 2; CLEAR: both; FONT-SIZE: 10pt; PADDING-TOP: 0px"" id=Normalheader class=immessageheader xmlns:convItem=""http://schemas.microsoft.com/2008/10/sip/convItems"" xmlns=""http://schemas.microsoft.com/2008/10/sip/convItems"" xmlns:rtc=""urn:microsoft-rtc-xslt-functions"" xmlns:msxsl=""urn:schemas-microsoft-com:xslt"" xmlns:xs=""http://www.w3.org/2001/XMLSchema""><SPAN style=""WHITE-SPACE: nowrap; FLOAT: right; COLOR: #666666; FONT-SIZE: 8pt; PADDING-TOP: 2px"" id=imsendtimestamp>{0}</SPAN><SPAN style=""FLOAT: left; COLOR: #666666"" id=imsendname>{1}</SPAN><SPAN style=""CLEAR: both""></SPAN></DIV>
<DIV style=""POSITION: relative; PADDING-BOTTOM: 0px; PADDING-LEFT: 3px; WIDTH: 100%; PADDING-RIGHT: 3px; CLEAR: both; PADDING-TOP: 0px"" id=Normalcontent xmlns:convItem=""http://schemas.microsoft.com/2008/10/sip/convItems"" xmlns=""http://schemas.microsoft.com/2008/10/sip/convItems"" xmlns:rtc=""urn:microsoft-rtc-xslt-functions"" xmlns:msxsl=""urn:schemas-microsoft-com:xslt"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
<DIV style=""FLOAT: left; HEIGHT: 100%; MARGIN-LEFT: 5px"" id=imwidget>
<OBJECT tabIndex=-1 classid=clsid:b3913e54-389f-45ea-9a3c-56b74cd62307><PARAM NAME=""_cx"" VALUE=""159""><PARAM NAME=""_cy"" VALUE=""318""><PARAM NAME=""EmoticonID"" VALUE=""114""></OBJECT></DIV>
<DIV style=""MARGIN-LEFT: 12px"" id=imcontent><SPAN>
<DIV style=""FONT-FAMILY: MS Shell Dlg 2; DIRECTION: ltr; COLOR: #000000; FONT-SIZE: 9pt"">{2}</DIV></SPAN></DIV></DIV></DIV>
<DIV>";

        protected static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public abstract string Keyword { get; set; }

        private int pageSize = 100;
        protected int PageSize 
        {
            get { return pageSize; }
            set 
            {
                pageSize = value;
                messageStore.PageSize = pageSize;
            }
        }

        public MessageProvider(IMessageStore messageStore)
        {
            this.messageStore = messageStore;
            this.messageStore.PageSize = pageSize;
        }

        public List<Contact> ContractList = new List<Contact>();

        public abstract void LoadContacts();

        public abstract int GetTotalDailyMessageCount(Contact contract);

        public abstract List<DateTime> GetConversationDateList(
            Contact contract,
            DateTime? searchDate,
            SearchDirection direction);

        public string GetDailyConversation(Contact contract, DateTime date)
        {
            var messageList = messageStore.GetMessage(contract, date, date.AddDays(1));

            if (messageList != null)
            {
                string content = null;
                StringBuilder sb = new StringBuilder();
                foreach (var msg in messageList)
                {
                    content = PrettyFormat(msg.MessageText);
                    content = ReplaceHistory(content);
                    
                    sb.Append("<p>");
                    sb.AppendLine(content);
                    sb.Append("</p>");
                }

                string html = "<html><head><meta HTTP-EQUIV=\"Content-Type\" content=\"text/html; charset=utf-8\" /> <script type=\"text/javascript\">function pointToBottom(){  window.location = \"#bottomlink\";}</script></head> <body onLoad=\"pointToBottom()\">"
                            + sb.ToString()
                            + "<a name=\"bottomlink\">&nbsp;</a></body></html>";

                return html;
            }

            return string.Empty;
        }

        private string ReplaceBetween(string s, string begin, string end, string replace)
        {
            Regex regex = new Regex(string.Format("\\{0}.*?\\{1}", begin, end));
            return regex.Replace(s, replace);
        }

        private string ReplaceHistory(string history)
        {
            history = ReplaceBetween(history, "<OBJECT", "</OBJECT>", "&#149");//"&#8227"
            history = history.Replace("<A ", "<A target=\"blank\" ");
            history = history.Replace(Environment.NewLine, "<br/>");
            return history;
        }

        private string PrettyFormat(string message)
        {
            string[] m = message.Split(new string[]{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);

            if(m.Length > 1)
            {
                string title= m[0];
                int i = title.IndexOf(']');

                string date = null;
                string sender = null;

                if(i > 0)
                {
                    i++;
                    date = title.Substring(0, i).Trim('[', ']');
                    sender = title.Substring(i).Trim();
                }

                StringBuilder sb = new StringBuilder();
                for(int n = 1; n < m.Length; n++)
                {
                    sb.AppendLine(m[n]);
                }

                string html = string.Format(PRETTY_MESSAGE_FORMAT, date, sender, sb.ToString());
                return html;
            }
            return string.Format(PRETTY_MESSAGE, message);
        }
    }
}
