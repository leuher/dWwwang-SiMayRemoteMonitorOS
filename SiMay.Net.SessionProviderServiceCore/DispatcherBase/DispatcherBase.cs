﻿using SiMay.Net.SessionProvider.Core;
using SiMay.Sockets.Tcp.Session;
using System;
using System.Collections.Generic;

namespace SiMay.Net.SessionProviderServiceCore
{
    public abstract class DispatcherBase : IDisposable
    {
        /// <summary>
        /// 当前连接会话
        /// </summary>
        protected TcpSocketSaeaSession CurrentSession
        {
            get;
            set;
        }

        private long? _dispatcherId;

        /// <summary>
        /// Id
        /// </summary>
        public virtual long DispatcherId
        {
            get
            {
                if (_dispatcherId.HasValue)
                    return _dispatcherId.Value;
                else
                {
                    _dispatcherId = this.GetHashCode();
                    return _dispatcherId.Value;
                }
            }
            set
            {
                _dispatcherId = value;
            }
        }

        /// <summary>
        /// 连接工作类型
        /// </summary>
        public virtual ConnectionWorkType ConnectionWorkType
        {
            get;
            set;
        } = ConnectionWorkType.None;

        /// <summary>
        /// 缓冲区
        /// </summary>
        public List<byte> ListByteBuffer
        {
            get;
            set;
        } = new List<byte>();

        /// <summary>
        /// 设置连接会话
        /// </summary>
        /// <param name="session"></param>
        public virtual void SetSession(TcpSocketSaeaSession session)
        {
            CurrentSession = session;

            session.AppTokens = new object[]
            {
                this,
                ConnectionWorkType
            };
        }

        /// <summary>
        /// 获取当前会话
        /// </summary>
        public TcpSocketSaeaSession GetCurrentSession()
        {
            return CurrentSession;
        }

        /// <summary>
        /// 关闭当前会话
        /// </summary>
        public virtual void CloseSession()
        {
            CurrentSession.Close(true);
        }

        /// <summary>
        /// 触发消息处理
        /// </summary>
        public abstract void OnMessage();

        /// <summary>
        /// 会话关闭
        /// </summary>
        public abstract void OnClosed();


        public virtual void Dispose()
        {
            ListByteBuffer.Clear();
        }
    }
}
