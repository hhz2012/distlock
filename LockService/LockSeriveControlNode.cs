﻿using LockQueueLib;
using Raft;
using Raft.Core.RaftEmulator;
using Raft.Transport;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LockService
{
    public class LockSeriveControlNode
    {
        TcpRaftNode trn = null;
        string nodeName = null;
        IWarningLog logger = null;
        LockTable table = new LockTable();
        public LockSeriveControlNode(string nodeName, NodeSettings setting,int Port,string localPath,IWarningLog logger)
        {
            
            this.nodeName = nodeName;
            this.logger = logger;
            trn = new TcpRaftNode(setting,
                                 localPath,              
                                 new LockClusterManagerHandler(this),
                                 Port,
                                 nodeName+"_Control",
                                 logger);
        }
        public void Start()
        {
            trn.Start();
        }
        public async Task StartConnect()
        {
            await trn.StartConnect();
        }
        public TcpRaftNode InnerNode
        {
            get
            {
                return this.trn;
            }
        }
        private TcpRaftNode wrk = null;
        public TcpRaftNode WorkNode
        {
            get
            {
                return this.wrk;
            }
        }
        public async Task JoinShard(ClusterCommand command)
        {
            //create a node from name and start the network
            await StartWorkNode(command);

        }
        public async Task<bool> DoWork(LockOper oper)
        {
            return this.table.GetQueue(oper.Key).LockNoWait(oper.Session, LockType.Read);
        }
        public async Task StartWorkNode(ClusterCommand command)
        {
            //create 
            RaftEntitySettings re_settings = null;
            List<int> ipAddress = new List<int>();
            for (int i = 0; i < command.IpAddress.Count; i++)
            {
                ipAddress.Add(command.IpAddress[i].port);
            }
            re_settings = new RaftEntitySettings()
            {
           //     VerboseRaft = true,
           //     VerboseTransport = true,
                DelayedPersistenceIsActive = true,
            };
            List<LockSeriveControlNode> nodes = new List<LockSeriveControlNode>();
            List<TcpClusterEndPoint> eps = new List<TcpClusterEndPoint>();
            //every node have seperate configuration
            var order = command.Targets.IndexOf(this.nodeName);
            for (int index = 0; index < command.IpAddress.Count; index++)
                    eps.Add(new TcpClusterEndPoint() { Host = "127.0.0.1", Port = ipAddress[index] });
            int Port = eps[order].Port;
            var nodeName = this.nodeName + "_worker";
            this.wrk=new TcpRaftNode(
                                   new NodeSettings()
                                   {
                                       TcpClusterEndPoints = eps,
                                       RaftEntitiesSettings = new List<RaftEntitySettings>() { re_settings }
                                   },
                                  LockClusterManager.PathRoot+nodeName,
                                  new WorkerHandler(this),
                                  Port,
                                  nodeName + "_Control",
                                  logger);
            wrk.Start();
            
            await Task.Delay(2000);
            await wrk.StartConnect();
            
        }
    }
}
