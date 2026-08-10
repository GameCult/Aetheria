/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;

public static class RethinkConnection
{
    public static RethinkDB R = RethinkDB.R;
    
    public static RethinkQueryStatus RethinkConnect(CultCache cache, string connectionString, string dbName, bool syncLocalChanges = true)
    {
        var status = new RethinkQueryStatus();


        return status;
    }
}


public class RethinkQueryStatus
{
    public int RetrievedEntries;
    public int TotalEntries;
}