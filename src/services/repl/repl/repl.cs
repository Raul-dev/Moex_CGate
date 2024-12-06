/*
        Copyright (c) 2014, MOEX. All rights reserved.
        
        Plaza-2 Client Gate API Usage Sample.
        Replication DataStream Client.
        
        All the software and documentation included in this and any
        other MOEX CGate Releasese is copyrighted by MOEX.
        
        Redistribution and use in source and binary forms, with or without
        modification, are permitted only by the terms of a valid
        software license agreement with MOEX.
        
        THIS SOFTWARE IS PROVIDED "AS IS" AND MICEX-RTS DISCLAIMS ALL WARRANTIES
        EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION, ANY IMPLIED WARRANTIES OF
        NON-INFRINGEMENT, MERCHANTABILITY OR FITNESS FOR A PARTICULAR
        PURPOSE.  MICEX-RTS DOES NOT WARRANT THAT USE OF THE SOFTWARE WILL BE
        UNINTERRUPTED OR ERROR-FREE.  MICEX-RTS SHALL NOT, UNDER ANY CIRCUMSTANCES, BE
        LIABLE TO LICENSEE FOR LOST PROFITS, CONSEQUENTIAL, INCIDENTAL, SPECIAL OR
        INDIRECT DAMAGES ARISING OUT OF OR RELATED TO THIS AGREEMENT OR THE
        TRANSACTIONS CONTEMPLATED HEREUNDER, EVEN IF MICEX-RTS HAS BEEN APPRISED OF
        THE LIKELIHOOD OF SUCH DAMAGES.
*/

/* 
    Repl - получение реплики данных по потоку. Пример печатает все получаемые сообщения в log. При разрыве соединения реплика
    начинается сначала.

    Repl allows to receive data replica for a stream and saves all incoming messages into log file. 
    When disconnected, the replica starts over.
*/

using System;
using System.Collections.Generic;
using System.Text;

using ru.micexrts.cgate;
using System.Runtime.InteropServices;
using ru.micexrts.cgate.message;

namespace repl
{
    class Repl
    {

        static bool bExit = false;

        // This callback may be used to test cg_msg_dump function
        // Dumps all the messages it receives 
        public static int MessageHandlerClientSimple(Connection conn, Listener listener, Message msg)
        {
            try
            {
                SchemeDesc schemeDesc = listener.Scheme;
                System.Console.WriteLine("Message {0}", msg);
                if (msg.Data != null)
                {
                    System.Console.WriteLine("data size = {0}", msg.Data.Length);
                }
                if (msg.Type == MessageType.MsgStreamData)
                {
                    StreamDataMessage smsg = (StreamDataMessage)msg;
                    System.Console.WriteLine("Table {0}", smsg.MsgName);
                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;                
            }
        }

        // This callback is what typically message callback looks like
        // Processes messages that are important to Plaza-2 datastream lifecycle
        public static int MessageHandlerClient(Connection conn, Listener listener, Message msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.MsgStreamData:
                        {
                            StreamDataMessage replmsg = (StreamDataMessage)msg;
                            Console.WriteLine(String.Format("DATA message SEQ={0} [table:[idx={1}, id={2}, name={3}], dataSize:{4}]", replmsg.Rev, replmsg.MsgIndex, replmsg.MsgId, replmsg.MsgName, msg.Data.Length));
                            break;
                        }
                    case MessageType.MsgP2ReplOnline:
                        {
                            Console.WriteLine("ONLINE");
                            break;
                        }
                    case MessageType.MsgTnBegin:
                        {
                            Console.WriteLine("TN Begin");
                            break;
                        }
                    case MessageType.MsgTnCommit:
                        {
                            Console.WriteLine("TN Commit");
                            break;
                        }
                    case MessageType.MsgOpen:
                        {
                            Console.WriteLine("OPEN");
                            {
                                SchemeDesc schemeDesc = listener.Scheme;
                                if (schemeDesc != null)
                                {
                                    foreach (MessageDesc messageDesc in schemeDesc.Messages)
                                    {
                                        Console.WriteLine(String.Format("Message {0}, block size = {1}", messageDesc.Name, messageDesc.Size));
                                        foreach (FieldDesc fieldDesc in messageDesc.Fields)
                                        {
                                            Console.WriteLine(String.Format("Field {0} = {1} [size={2}, offset={3}]", fieldDesc.Name, fieldDesc.Type, fieldDesc.Size, fieldDesc.Offset));
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    case MessageType.MsgClose:
                        {
                            Console.WriteLine("CLOSE");
                            break;
                        }
                    case MessageType.MsgP2ReplLifeNum:
                        {
                            Console.WriteLine(String.Format("Life number changed to: {0}", ((P2ReplLifeNumMessage)msg).LifeNumber));
                            break;
                        }
                    case MessageType.MsgP2ReplClearDeleted:
                        {
                            P2ReplClearDeletedMessage cdMessage = (P2ReplClearDeletedMessage)msg;
                            Console.WriteLine(String.Format("Clear deleted: table {0}, revision {1}", cdMessage.TableIdx, cdMessage.TableRev));
                            if (cdMessage.TableRev == P2ReplClearDeletedMessage.MaxRevision)
                            {
                                Console.WriteLine(String.Format("Table {0}, rev start from 1", cdMessage.TableIdx));
                            }
                            break;
                        }
                    case MessageType.MsgP2ReplReplState:
                        {
                            Console.WriteLine(String.Format("Message {0}", ((P2ReplStateMessage)msg).ReplState));
                            break;
                        }
                    default:
                        {
                            Console.WriteLine(String.Format("Message {0}", msg.Type));
                            break;
                        }

                }
                return 0;
            }
            catch (CGateException e)
            {
                return (int)e.ErrCode;
            }
        }

        public static void ConsoleCancelEventHandler(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            bExit = true;
        }

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += ConsoleCancelEventHandler;
            
            CGate.Open("ini=netrepl.ini;key=11111111");
            CGate.LogInfo("test .Net log.");
            Connection conn = new Connection("p2tcp://127.0.0.1:4001;app_name=ntest_repl");

            String[] lsnsStr = new String[] { 
                "p2ordbook://FORTS_ORDLOG_REPL;snapshot=FORTS_USERORDERBOOK_REPL", 
                "p2repl://FORTS_REFDATA_REPL;scheme=|FILE|refdata.ini|CustReplScheme", 
                "p2repl://FORTS_TRADE_REPL",
                "p2repl://FORTS_TRADE_REPL;scheme=|FILE|trades.ini|CustReplScheme",
                "p2ordbook://FORTS_TRADE_REPL;snapshot=FORTS_USERORDERBOOK_REPL;online.scheme=|FILE|trades.ini|CustReplScheme",
                "p2ordbook://FORTS_ORDLOG_REPL;snapshot=FORTS_USERORDERBOOK_REPL;online.scheme=|FILE|ordLog_trades.ini|CustReplScheme",
                "p2ordbook://FORTS_TRADE_REPL;snapshot=FORTS_USERORDERBOOK_REPL",
            };
            int idx = 0;
            bool outHelp = false;
            if (args.Length >= 1)
            {
                try
                {
                    idx = Int32.Parse(args[0]);
                    if (idx >= lsnsStr.Length)
                    {
                        outHelp = true;
                        System.Console.WriteLine("Index of listener string is out of range.");
                        idx = 0;
                    }
                }
                catch(Exception)
                {
                    System.Console.WriteLine("Please, define index of listener string in 1 argument. Convert error.");
                    outHelp = true;
                }
            }
            else
        {
                System.Console.WriteLine("Please, define index of listener string in 1 argument.");
                outHelp = true;
        }
            if (outHelp)
            {
                for (int i = 0; i < lsnsStr.Length; ++i)
                    System.Console.WriteLine(String.Format("{0}: {1}", i, lsnsStr[i]));
            }
            Listener listener = new Listener(conn, lsnsStr[idx]);
            listener.Handler += new Listener.MessageHandler(MessageHandlerClient);
            while (!bExit)
            {
                try
                {
                    State state = conn.State;
                    if (state == State.Error)
                    {
                        conn.Close();
                    }
                    else if (state == State.Closed)
                    {
                        conn.Open("");
                    }
                    else if (state == State.Opening)
                    {
                        ErrorCode result = conn.Process(0);
                        if (result != ErrorCode.Ok && result != ErrorCode.TimeOut)
                        {
                            CGate.LogError(String.Format("Warning: connection state request failed: {0}", CGate.GetErrorDesc(result)));
                        }
                    }
                    else if (state == State.Active)
                    {
                        ErrorCode result = conn.Process(0);
                        if (result != ErrorCode.Ok && result != ErrorCode.TimeOut)
                        {
                            CGate.LogError(String.Format("Warning: connection state request failed: {0}", CGate.GetErrorDesc(result)));
                        }
                        if (listener.State == State.Closed)
                        {
                            listener.Open("");
                        }
                        else if (listener.State == State.Error)
                        {
                            listener.Close();
                        }

                    }
                }
                catch (CGateException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            listener.Close();
            conn.Close();
            listener.Dispose();
            conn.Dispose();
            CGate.Close();
        }
    }
}
