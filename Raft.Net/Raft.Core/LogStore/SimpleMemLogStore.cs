﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Raft.Core.LogStore
{
    public class SimpleMemLogStore : IStateLog
    {
        RaftStateMachine stateMachine = null;
        public SimpleMemLogStore(RaftStateMachine rn,string path)
        {
            this.stateMachine = rn;
        }
        public ulong StateLogId { get; set; }
        public ulong StateLogTerm { get; set; }
        public ulong LastCommittedIndex { get; set; }
        public ulong LastCommittedIndexTerm { get; set; }
        public ulong LastBusinessLogicCommittedIndex { get; set; }
        public ulong LastAppliedIndex { get; set; }

        public ulong PreviousStateLogId = 0;
        public ulong PreviousStateLogTerm = 0;

        private List<StateLogEntry> list = new List<StateLogEntry>();

        public void AddFakePreviousRecordForInMemoryLatestEntity(ulong prevIndex, ulong prevTerm)
        {
            throw new NotImplementedException();
        }

        public void AddLogEntry(StateLogEntrySuggestion suggestion)
        {
            PreviousStateLogId = suggestion.StateLogEntry.PreviousStateLogId;
            PreviousStateLogTerm = suggestion.StateLogEntry.PreviousStateLogTerm;
            StateLogId = suggestion.StateLogEntry.Index;
            StateLogTerm = suggestion.StateLogEntry.Term;
            this.list.Add(suggestion.StateLogEntry);
        }

        public void AddLogEntryByFollower(StateLogEntrySuggestion suggestion)
        {
            //remove all log bigger than this,(clear no committed logs)
            list.RemoveAll(s => s.Term >= suggestion.StateLogEntry.Term || s.Index > suggestion.LeaderTerm);
            //add this one
            AddLogEntry(suggestion);
            //update commit status
            if (suggestion.IsCommitted)
            {
                if (this.LastCommittedIndexTerm > suggestion.StateLogEntry.Term
                    ||(
                        this.LastCommittedIndexTerm == suggestion.StateLogEntry.Term
                        &&this.LastCommittedIndex > suggestion.StateLogEntry.Index
                       ))
                {
                    //Should be not possible
                }
                else
                {
                    this.LastCommittedIndex = suggestion.StateLogEntry.Index;
                    this.LastCommittedIndexTerm = suggestion.StateLogEntry.Term;
                }
            }
        }

        public bool CommitLogEntry(NodeRaftAddress address, uint majorityQuantity, StateLogEntryApplied applied)
        {
            //If we receive acceptance signals of already Committed entries, we just ignore them
            if (this.LastCommittedIndex < applied.StateLogEntryId && stateMachine.NodeTerm == applied.StateLogEntryTerm)    //Setting LastCommittedId
            {
                var tocommit = list.FindAll(s => s.Term == applied.StateLogEntryTerm
                               && s.Index <= applied.StateLogEntryId
                               && s.IsCommitted == false);
                if (tocommit.Count>0)
                {
                    tocommit.ForEach(s => s.IsCommitted = true);
                }
                return tocommit.Count > 0;
            }
            return false;
        }

        public void Debug_PrintOutInMemory()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public StateLogEntry GetCommitedEntryByIndex(ulong logEntryId)
        {
            return list.Find(s => s.Index == logEntryId && s.IsCommitted == true);
        }

        public StateLogEntry GetEntryByIndexTerm(ulong logEntryId, ulong logEntryTerm)
        {
            return list.Find(s => s.Index == logEntryId && s.Index == logEntryTerm);
        }

        public StateLogEntrySuggestion GetNextStateLogEntrySuggestion(StateLogEntryRequest req)
        {
            StateLogEntrySuggestion le = new StateLogEntrySuggestion()
            {
                LeaderTerm = this.stateMachine.NodeTerm
            };
            ulong prevId = 0;
            ulong prevTerm = 0;
            StateLogEntry entry = null;
            if (req.StateLogEntryId == 0)
            {
                //send first record to sync
                entry = list.Find(s => s.IsCommitted == true);
            }
            else
            { 
                for (int index = 0; index <list.Count; index++)
                {
                    if (list[index].Index >req.StateLogEntryId)
                    {
                        //find next one and set preview value
                        if (index>0)
                        {
                            prevId = list[index - 1].Index;
                            prevTerm = list[index - 1].Term;
                            entry = list[index];
                        }
                    }
                }
            }
            if (entry != null)
            {
                le.StateLogEntry = entry;
                entry.PreviousStateLogId = prevId;
                entry.PreviousStateLogTerm = prevTerm;
                le.IsCommitted = entry.IsCommitted;
                return le;
            }
            else return null;

        }

        public void ReloadFromStorage()
        {
            //should load from log files
           

        }

        public void RollbackToLastestCommit()
        {
            if (LastCommittedIndex == 0 || LastCommittedIndexTerm == 0)
                return;
            list.RemoveAll(s => s.Term >= LastCommittedIndexTerm && s.Index > LastCommittedIndex);
        }

        public SyncResult SyncCommitByHeartBeat(LeaderHeartbeat lhb)
        {
            if (this.LastCommittedIndex < lhb.LastStateLogCommittedIndex)
            {
                //find if this entry exist 
                var entry = list.Find(s => s.Term == lhb.LastStateLogCommittedIndexTerm && s.Index == lhb.StateLogLatestIndex);
                if (entry != null)
                {
                    if (!entry.IsCommitted)
                    {
                        entry.IsCommitted = true;
                        return new SyncResult() { HasCommit = true, Synced = true };
                    }
                    else
                    {
                        return new SyncResult() { HasCommit = false, Synced = true };
                    }
                }
                else
                {
                    return new SyncResult() { HasCommit = false, Synced = false };
                }
            }
            else
            {
                return new SyncResult() { HasCommit = false, Synced = true };
            }
        }
    }

      
    }
