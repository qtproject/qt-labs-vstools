﻿/****************************************************************************
**
** Copyright (C) 2018 The Qt Company Ltd.
** Contact: https://www.qt.io/licensing/
**
** This file is part of the Qt VS Tools.
**
** $QT_BEGIN_LICENSE:GPL-EXCEPT$
** Commercial License Usage
** Licensees holding valid commercial Qt licenses may use this file in
** accordance with the commercial license agreement provided with the
** Software or, alternatively, in accordance with the terms contained in
** a written agreement between you and The Qt Company. For licensing terms
** and conditions see https://www.qt.io/terms-conditions. For further
** information use the contact form at https://www.qt.io/contact-us.
**
** GNU General Public License Usage
** Alternatively, this file may be used under the terms of the GNU
** General Public License version 3 as published by the Free Software
** Foundation with exceptions as appearing in the file LICENSE.GPL3-EXCEPT
** included in the packaging of this file. Please review the following
** information to ensure the GNU General Public License requirements will
** be met: https://www.gnu.org/licenses/gpl-3.0.html.
**
** $QT_END_LICENSE$
**
****************************************************************************/

using System;
using System.Runtime.Serialization;

namespace QtVsTools.Qml.Debug.V4
{
    using Json;

    [DataContract]
    class Message : Serializable<Message>
    {
        //  <message type>
        //  ...
        protected Message()
        { }

        public static T Create<T>(ProtocolDriver driver)
            where T : Message, new()
        {
            return new T { Driver = driver };
        }

        public static bool Send<T>(ProtocolDriver driver, Action<T> initMsg = null)
            where T : Message, new()
        {
            T _this = Create<T>(driver);
            if (initMsg != null)
                initMsg(_this);
            return _this.Send();
        }

        public string Type { get; set; }

        protected sealed override void InitializeObject(object initArgs)
        {
            if (initArgs is string)
                Type = initArgs as string;
        }

        protected override bool? IsCompatible(Message that)
        {
            System.Diagnostics.Debug.Assert(IsPrototype);

            if (that == null)
                return false;
            if (string.IsNullOrEmpty(Type))
                return null;
            return (this.Type == that.Type);
        }

        protected ProtocolDriver Driver { get; private set; }

        public virtual bool Send()
        {
            return Driver.SendMessage(this);
        }
    }

    [DataContract]
    class Request : Message
    {
        //  "v8request"
        //  { "seq"     : <number>,
        //    "type"    : "request",
        //    "command" : <command>
        //    ...
        //  }
        public const string MSG_TYPE = "v8request";
        public const string MSG_SUBTYPE = "request";
        protected Request() : base()
        {
            Type = MSG_TYPE;
            SubType = MSG_SUBTYPE;
        }

        [DataMember(Name = "type")]
        public string SubType { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "seq")]
        public int SequenceNum { get; set; }

        protected override bool? IsCompatible(Message msg)
        {
            System.Diagnostics.Debug.Assert(IsPrototype);

            if (base.IsCompatible(msg) == false)
                return false;

            var that = msg as Request;
            if (that == null)
                return null;

            if (string.IsNullOrEmpty(SubType))
                return null;

            if (string.IsNullOrEmpty(Command))
                return null;

            return this.SubType == that.SubType && this.Command == that.Command;
        }

        Response response = null;
        public Response Response
        {
            get { return response; }
            set { Atomic(() => response == null, () => response = value); }
        }

        object tag = null;
        public object Tag
        {
            get { return tag; }
            set { Atomic(() => tag == null, () => tag = value); }
        }

        public virtual ProtocolDriver.PendingRequest SendAsync()
        {
            return Driver.SendRequest(this);
        }

        public new Response Send()
        {
            return SendAsync().WaitForResponse();
        }

        public static new Response Send<T>(ProtocolDriver driver, Action<T> initMsg = null)
            where T : Request, new()
        {
            T _this = Create<T>(driver);
            if (initMsg != null)
                initMsg(_this);
            return _this.Send();
        }
    }

    [DataContract]
    [SkipDeserialization]
    class Request<TResponse> : Request
        where TResponse : Response
    {
        public new TResponse Response
        { get { return base.Response as TResponse; } }

        public new virtual TResponse Send()
        {
            var pendingRequest = SendAsync();
            if (!pendingRequest.RequestSent)
                return null;

            if (pendingRequest.WaitForResponse() == null)
                return null;

            return Response;
        }
    }

    [DataContract]
    [SkipDeserialization]
    class Request<TResponse, TArgs> : Request<TResponse>
        where TResponse : Response
        where TArgs : class, new()
    {
        [DataMember(Name = "arguments", EmitDefaultValue = false)]
        TArgs args = null;

        public virtual TArgs Arguments
        {
            get { return ThreadSafe(() => (args != null) ? args : (args = new TArgs())); }
            set { args = value; }
        }
    }

    [DataContract]
    class ServerMessage : Message
    {
        //  "v8message"
        //  { "seq"  : <number>,
        //    "type" : <string>
        //    ...
        //  }
        public const string MSG_TYPE = "v8message";
        protected ServerMessage() : base()
        {
            Type = MSG_TYPE;
        }

        [DataMember(Name = "type")]
        public string SubType { get; set; }

        [DataMember(Name = "seq")]
        public int SequenceNum { get; set; }

        protected override bool? IsCompatible(Message msg)
        {
            System.Diagnostics.Debug.Assert(IsPrototype);

            if (base.IsCompatible(msg) == false)
                return false;

            var that = msg as ServerMessage;
            if (that == null)
                return null;

            if (string.IsNullOrEmpty(SubType))
                return null;

            return this.SubType == that.SubType;
        }
    }

    [DataContract]
    class Response : ServerMessage
    {
        //  "v8message"
        //  { "seq"         : <number>,
        //    "type"        : "response",
        //    "request_seq" : <number>,
        //    "command"     : <command>,
        //    "running"     : <is the VM running after sending this response>,
        //    "success"     : <success?>,
        //    "message"     : [error message]
        //    ...
        //  }
        public const string MSG_SUBTYPE = "response";
        protected Response() : base()
        {
            SubType = MSG_SUBTYPE;
        }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "request_seq")]
        public int RequestSeq { get; set; }

        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "running")]
        public bool Running { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        protected override bool? IsCompatible(Message msg)
        {
            System.Diagnostics.Debug.Assert(IsPrototype);

            if (base.IsCompatible(msg) == false)
                return false;

            var that = msg as Response;
            if (that == null)
                return null;

            // If response is unsuccessful, no need to continue searching, just use this class,
            // it already has all the data needed for error processing (i.e. the error message)
            if (!that.Success)
                return true;

            if (string.IsNullOrEmpty(Command))
                return null;

            return this.Command == that.Command;
        }
    }

    [DataContract]
    [SkipDeserialization]
    class Response<TBody> : Response
        where TBody : class, new()
    {
        [DataMember(Name = "body", EmitDefaultValue = false)]
        TBody body = null;

        public TBody Body
        {
            get { return ThreadSafe(() => (body != null) ? body : (body = new TBody())); }
            set { body = value; }
        }
    }

    [DataContract]
    class Event : ServerMessage
    {
        //  "v8message"
        //  { "seq"   : <number>,
        //    "type"  : "event",
        //    "event" : <string>,
        //    ...
        //  }
        public const string MSG_SUBTYPE = "event";
        protected Event() : base()
        {
            SubType = MSG_SUBTYPE;
        }

        [DataMember(Name = "event")]
        public string EventType { get; set; }

        protected override bool? IsCompatible(Message msg)
        {
            System.Diagnostics.Debug.Assert(IsPrototype);

            if (base.IsCompatible(msg) == false)
                return false;

            var that = msg as Event;
            if (that == null)
                return null;

            if (string.IsNullOrEmpty(EventType))
                return null;

            return this.EventType == that.EventType;
        }
    }

    [DataContract]
    [SkipDeserialization]
    class Event<TBody> : Event
        where TBody : class, new()
    {
        [DataMember(Name = "body", EmitDefaultValue = false)]
        TBody body = null;

        public TBody Body
        {
            get { return ThreadSafe(() => (body != null) ? body : (body = new TBody())); }
            set { body = value; }
        }
    }
}
