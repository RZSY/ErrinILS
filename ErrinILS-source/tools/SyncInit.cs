using System;
using System.IO;
using Errin;

namespace ErrinTools
{
    class SyncInit
    {
        static void Main()
        {
            var store = new Store(null);
            var db = store.Load();
            if (db == null)
            {
                Console.WriteLine("Failed to load db from " + store.DataFile);
                return;
            }
            Console.WriteLine("Loaded database from " + store.DataFile);
            Console.WriteLine("Items count: " + db.Items.Count);
            Console.WriteLine("Patrons count: " + db.Patrons.Count);

            if (string.IsNullOrEmpty(db.Set.school))
                db.Set.school = "SK Taman Ria";

            // Run Sync
            Console.WriteLine("Running CloudSyncManager.Instance.SyncAll...");
            var res = CloudSyncManager.Instance.SyncAll(db, store);
            Console.WriteLine("Success: " + res.Success);
            Console.WriteLine("Message: " + res.Message);
        }
    }
}
