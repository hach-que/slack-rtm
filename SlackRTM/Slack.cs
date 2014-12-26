﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlackRTM.Events;
using SlackRTM.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WebSocketSharp;

namespace SlackRTM
{
    /// <summary>
    /// The core Slack class.
    /// </summary>
    public class Slack
    {
        public TeamInfo TeamInfo { get; private set; }

        private WebClient wc = new WebClient();

        private WebSocket webSocket;

        public List<Channel> Channels { get; set; }

        public User Self { get; set; }

        public List<User> Users { get; set; }

        private string Url { get; set; }
        public Slack()
        {
            OnAck += Slack_OnAck;
        }

        void Slack_OnAck(object sender, SlackEventArgs e)
        {
            var ack = (e.Data as Ack);
            
        }
        public bool Init(string token)
        {
            JObject response = JObject.Parse(wc.DownloadString(
                new UriBuilder("https://slack.com/api/rtm.start") { Query = "token=" + token }.Uri));
            if (response["ok"].Value<bool>() == false)
            {
                Console.WriteLine(response["error"]);
                return false;
            }
            TeamInfo = JsonConvert.DeserializeObject<TeamInfo>(response["team"].ToString(), new SlackJsonConverter());
            Channels = JsonConvert.DeserializeObject<List<Channel>>(response["channels"].ToString(), new SlackJsonConverter());
            Users = JsonConvert.DeserializeObject<List<User>>(response["users"].ToString(), new SlackJsonConverter());
            Self = Users.First(n => n.Id == response["self"]["id"].ToString());
            Url = response["url"].ToString();
            //TODO: Groups, Bots and IMs.
            return true;
        }

        public bool Connect()
        {
            if (String.IsNullOrEmpty(Url))
                throw new InvalidOperationException("Call Slack.Init() first.");
            webSocket = new WebSocket(Url);
            webSocket.OnMessage += webSocket_OnMessage;
            webSocket.Connect();
            
            return true;
        }

        void webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            var data = Event.NewEvent(e.Data);
            if (data.Type == "hello")
                this.RecievedHello = true;
            if (data is Ack)
                this.OnAck(this, new SlackEventArgs(data));
            else if (this.OnEvent != null)
                this.OnEvent(this, new SlackEventArgs(data));
        }

        public delegate void OnEventEvent(object sender, SlackEventArgs e);
        public event OnEventEvent OnEvent;
        public event OnEventEvent OnAck;


        public bool RecievedHello { get; set; }

        public bool Connected { get { return webSocket.IsAlive; } }

        public Channel GetChannel(string p)
        {
            if (string.IsNullOrEmpty(p) || p == "#")
                return null; 
            if (p[0] == '#')
                p = p.Substring(1);
            return Channels.FirstOrDefault(c => c.Id == p || c.Name == p);
        }

        int sendId = 0;

        public void SendMessage(string channel, string text)
        {
            var chan = GetChannel(channel);
            //if (!chan.IsMember)
            //    throw new NotInChannelException();
            var message = new Message(chan.Id, text, sendId++);
            SentMessages.Add(message);
            webSocket.Send(message.ToJson());                            
        }

        List<Event> SentMessages = new List<Event>();

        public bool Connecting { get { return !RecievedHello && webSocket != null; } }

        public User GetUser(string p)
        {
            if (string.IsNullOrEmpty(p) || p == "@")
                return null;
            if (p[0] == '@')
                p = p.Substring(1);
            return Users.FirstOrDefault(c => c.Id == p || c.Name == p);
        }
    }
}