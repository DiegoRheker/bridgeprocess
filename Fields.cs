using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using SchutzAPI;

namespace SchutzAPI
{
    class Fields
    {
        //--0--Handle	
        //--1--Title	
        //--2--Body (HTML)	
        //--3--Vendor	
        //--4--Type	
        //--5--Tags	
        //--6--Published	
        //--7--Option1 Name	
        //--8--Option1 Value	
        //--9--Option2 Name	
        //--10--Option2 Value	
        //--11--Option3 Name	
        //--12--Option3 Value	
        //--13--Variant SKU	
        //--14--Variant Grams	
        //--15--Variant Inventory Tracker	
        //--16--Variant Inventory Qty	
        //--17--Variant Inventory Policy	
        //--18--Variant Fulfillment Service	
        //--19--Variant Price	
        //--20--Variant Compare At Price	
        //--21--Variant Requires Shipping	
        //--22--Variant Taxable	
        //--23--Variant Barcode	
        //--24--Image Src	
        //--25--Image Alt Text	
        //--26--Gift Card	
        //--27--Google Shopping / MPN	
        //--28--Google Shopping / Age Group	
        //--29--Google Shopping / Gender	
        //--30--Google Shopping / Google Product Category	
        //--31--SEO Title	
        //--32--SEO Description	
        //--33--Google Shopping / AdWords Grouping	
        //--34--Google Shopping / AdWords Labels	
        //--35--Google Shopping / Condition	
        //--36--Google Shopping / Custom Product	
        //--37--Google Shopping / Custom Label 0	
        //--38--Google Shopping / Custom Label 1	
        //--39--Google Shopping / Custom Label 2	
        //--40--Google Shopping / Custom Label 3	
        //--41--Google Shopping / Custom Label 4	
        //--42--Variant Image	
        //--43--Variant Weight Unit	
        //--44--Variant Tax Code
        private static string fld1 { get; set; }
        private static string fld2 { get; set; }
        private static string fld3 { get; set; }
        private static string fld4 { get; set; }
        private static string fld5 { get; set; }
        private static string fld6 { get; set; }
        private static string fld7 { get; set; }
        private static string fld8 { get; set; }
        private static string fld9 { get; set; }
        private static string fld10 { get; set; }
        private static string fld11 { get; set; }
        private static string fld12 { get; set; }
        private static string fld13 { get; set; }
        private static string fld14 { get; set; }
        private static string fld15 { get; set; }
        private static string fld16 { get; set; }
        private static string fld17 { get; set; }
        private static string fld18 { get; set; }
        private static string fld19 { get; set; }
        private static string fld20 { get; set; }
        private static string fld21 { get; set; }
        private static string fld22 { get; set; }
        private static string fld23 { get; set; }
        private static string fld24 { get; set; }
        private static string fld25 { get; set; }
        private static string fld26 { get; set; }
        private static string fld27 { get; set; }
        private static string fld28 { get; set; }
        private static string fld29 { get; set; }
        private static string fld30 { get; set; }
        private static string fld31 { get; set; }
        private static string fld32 { get; set; }
        private static string fld33 { get; set; }
        private static string fld34 { get; set; }
        private static string fld35 { get; set; }
        private static string fld36 { get; set; }
        private static string fld37 { get; set; }
        private static string fld38 { get; set; }
        private static string fld39 { get; set; }
        private static string fld40 { get; set; }
        private static string fld41 { get; set; }
        private static string fld42 { get; set; }
        private static string fld43 { get; set; }
        private static string fld44 { get; set; }

        static void ParseFile()
        {
            String line;
            StreamReader sr = new StreamReader(ConfigurationManager.AppSettings.Get("ProductsSourceFile"));
            while ((line = sr.ReadLine()) != null)
            {
                ParseRecord(line);
            }
        }

        static void ParseRecord(string line)
        {

            String[] split = line.Split(new Char[] { ',' });
            //Int32 x = 0;
            foreach (String s in split)
            {

            }

        }
    }
}
