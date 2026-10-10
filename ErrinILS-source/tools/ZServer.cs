using System; using System.Collections.Generic; using System.Threading; using Errin;
static class S { static void Main(){
  var items=new List<Item>{
   new Item{id="1",ctl="ER00000001",acq="ACQ-000001",barcode="B1001",title="Charlotte's Web",author="E. B. White",isbn="9780064400558",publisher="Harper",year="2001",call="F WHI",subject="Animals--Fiction",pages="184"},
   new Item{id="2",ctl="ER00000002",barcode="B1002",title="A Brief History of Time",sub="from the Big Bang to black holes",author="Stephen Hawking",isbn="9780553380163",publisher="Bantam",year="1998",call="523.1 HAW",subject="Cosmology",pages="212"},
   new Item{id="3",ctl="ER00000003",barcode="B1003",title="Hatchet",author="Gary Paulsen",isbn="9781416936473",year="2006",call="F PAU",subject="Wilderness survival--Fiction"}};
  var s=Z.StartServer(24999,true,"errin",()=>items,m=>Console.WriteLine("srv: "+m)); Console.WriteLine("listening"); Thread.Sleep(40000); s.Stop(); } }
