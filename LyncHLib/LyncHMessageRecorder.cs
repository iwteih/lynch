using ILyncH;
using log4net;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LyncHLib
{
    public class LyncHMessageRecorder
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private IMessageStore messageStore;
        private LyncClient lyncCLient = null;
        private LyncDaemon daemon = new LyncDaemon();

        public LyncHMessageRecorder(IMessageStore messageStore)
        {
            if (messageStore == null)
            {
                throw new ArgumentNullException("IMessageStore is null");
            }

            //if (notify == null)
            //{
            //    throw new ArgumentNullException("INotify is null");
            //}

            this.messageStore = messageStore;
            //this.notify = notify;
        }

        public void StartRecord()
        {
            try
            {
                daemon.OnCommunicatorRuning += daemon_OnCommunicatorRuning;
                daemon.OnCommunicatorNotRuning += daemon_OnCommunicatorNotRuning;
                daemon.StartMonitor();
            }
            catch (Exception exception)
            {
                logger.Error(exception);
            }
        }

        public void StopRecord()
        {
            if (daemon != null)
            {
                daemon.OnCommunicatorRuning -= daemon_OnCommunicatorRuning;
                daemon.OnCommunicatorNotRuning -= daemon_OnCommunicatorNotRuning;

                daemon.StopMonitor();
            }
        }
        
        void daemon_OnCommunicatorRuning(object sender, OCStatus state)
        {
            if (lyncCLient == null)
            {
                lyncCLient = LyncClient.GetClient();
                lyncCLient.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;
            }
        }

        void daemon_OnCommunicatorNotRuning(object sender, OCStatus state)
        {
            if(lyncCLient != null)
            {
                lyncCLient.ConversationManager.ConversationAdded -= ConversationManager_ConversationAdded;
            }
            lyncCLient = null;
        }

        void ConversationManager_ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            e.Conversation.ParticipantAdded += Conversation_ParticipantAdded;
        }

        void Conversation_ParticipantAdded(object sender, ParticipantCollectionChangedEventArgs e)
        {
            if (((Conversation)sender).Modalities.ContainsKey(ModalityTypes.InstantMessage))
            {
                ((InstantMessageModality)e.Participant.Modalities[ModalityTypes.InstantMessage]).InstantMessageReceived += LyncHMessageRecorder_InstantMessageReceived;
            }
        }

        void LyncHMessageRecorder_InstantMessageReceived(object sender, MessageSentEventArgs e)
        {
            var messageModality = sender as InstantMessageModality;

            if(messageModality == null)
            {
                logger.ErrorFormat("sender is not of type InstantMessageModality, sender={0}", sender);
                return;
            }
            
            string senderName = messageModality.Endpoint.DisplayName;
            string friendlyName = null;

            if(messageModality.Participant.Properties.ContainsKey(ParticipantProperty.Name))
            {
                friendlyName = messageModality.Participant.Properties[ParticipantProperty.Name].ToString();
            }
            else
            {
                friendlyName = senderName;
            }

            string message = e.Text.Trim();
            message = NormalizeMessage(friendlyName, message);

            List<string> participants = new List<string>();

            foreach(Participant p in messageModality.Conversation.Participants)
            {
                if (p.IsSelf)
                {
                    continue;
                }
                participants.Add(NormalizeAddress(p.Contact.Uri));
            }

            messageStore.SaveMessage(DateTime.Now, message, participants.ToArray());
        }

        private string NormalizeAddress(string participantUri)
        {
            return participantUri.Replace("sip:", string.Empty);
        }

        private string NormalizeMessage(string participant, string message)
        {
            return string.Format("[{0}] {1}{2}{3}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), participant, Environment.NewLine, message);
        }
    }
}
