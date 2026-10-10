using System; using Errin;
static class C { static int Main(string[] a){
  string host=a[0]; int port=int.Parse(a[1]); string db=a[2]; string field=a[3], term=a[4];
  try{ var r=Z.Search(new Z.SearchOpts{Host=host,Port=port,Db=db,Field=field,Term=term,Max=5,Timeout=6000});
    Console.WriteLine("OK count="+r.Count+" parsed="+r.Items.Count+" skipped="+r.Skipped); foreach(var i in r.Items) Console.WriteLine("  - "+i.title+" | "+i.author+" | "+i.isbn); return 0; }
  catch(Exception e){ Console.WriteLine("ERROR: "+e.Message); return 1; } } }
