// GtkSharp.Generation.CallbackGen.cs - The Callback Generatable.
//
// Author: Mike Kestner <mkestner@speakeasy.net>
//
// Copyright (c) 2002-2003 Mike Kestner
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the GNU General Public
// License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.


namespace GtkSharp.Generation {

	using System;
	using System.IO;
	using System.Xml;

	public class CallbackGen : GenBase {

		private Parameters parms;
		private Signature sig = null;
		private ImportSignature isig = null;
		private ReturnValue retval;

		public CallbackGen (XmlElement ns, XmlElement elem) : base (ns, elem) 
		{
			retval = new ReturnValue (elem ["return-type"]);
			parms = new Parameters (elem ["parameters"]);
			parms.HideData = true;
		}

		public override string MarshalType {
			get {
				return NS + "Sharp." + Name + "Native";
			}
		}

		public override string CallByName (string var_name)
		{
			return var_name + ".NativeDelegate";
		}

		public override string FromNative(string var)
		{
			return var;
		}

		public string GenWrapper (string ns, GenerationInfo gen_info)
		{
			string wrapper = Name + "Native";
			string qualname = ns + "Sharp." + wrapper;

			isig = new ImportSignature (parms, NS);

			StreamWriter sw = gen_info.OpenStream (qualname);

			sw.WriteLine ("namespace " + ns + "Sharp {");
			sw.WriteLine ();
			sw.WriteLine ("\tusing System;");
			sw.WriteLine ();
			sw.WriteLine ("#region Autogenerated code");
			sw.WriteLine ("\tinternal delegate " + retval.MarshalType + " " + wrapper + "(" + isig + ");");
			sw.WriteLine ();
			sw.WriteLine ("\tinternal class " + Name + "Wrapper {");
			sw.WriteLine ();
			sw.WriteLine ("\t\tpublic " + retval.MarshalType + " NativeCallback (" + isig + ")");
			sw.WriteLine ("\t\t{");

			bool need_sep = false;
			string call_str = "";
			string cleanup_str = "";
			for (int i = 0, idx = 0; i < parms.Count; i++)
			{
				Parameter p = parms [i];

				if (i > 0 && p.IsLength && parms[i-1].IsString)
					continue;

				if ((i == parms.Count - 1) && p.IsUserData) 
					continue;

				if (p.CType == "GError**") {
					sw.WriteLine ("\t\t\t" + p.Name + " = IntPtr.Zero;");
					continue;
				}

				IGeneratable gen = p.Generatable;

				sw.Write("\t\t\t" + p.CSType + " _arg" + idx);
				if (p.PassAs == "out") {
					sw.WriteLine(";");
					cleanup_str += "\t\t\t" + p.Name + " = " + gen.CallByName ("_arg" + idx) + ";\n";
				} else
					sw.WriteLine(" = " + gen.FromNative (p.Name) + ";");

				if (need_sep)
					call_str += ", ";
				else
					need_sep = true;
				call_str += String.Format ("{0} _arg{1}", p.PassAs, idx);
				idx++;
			}

			sw.Write ("\t\t\t");
			string invoke = "managed (" + call_str + ")";
			if (retval.MarshalType != "void") {
				if (cleanup_str == "")
					sw.Write ("return ");
				else {
					sw.Write (retval.MarshalType + " ret = ");
					cleanup_str += "\t\t\treturn ret;\n";
				}

				SymbolTable table = SymbolTable.Table;
				ClassBase ret_wrapper = table.GetClassGen (retval.CType);
				if (ret_wrapper != null && (ret_wrapper is ObjectGen || ret_wrapper is OpaqueGen))
					sw.WriteLine ("(({0}) {1}).Handle;", retval.CSType, invoke);
				else if (table.IsStruct (retval.CType) || table.IsBoxed (retval.CType)) {
					// Shoot. I have no idea what to do here.
					Console.WriteLine ("Struct return type {0} in callback {1}", retval.CType, CName);
					sw.WriteLine ("IntPtr.Zero;"); 
				} else if (table.IsEnum (retval.CType))
					sw.WriteLine ("(int) {0};", invoke);
				else
					sw.WriteLine ("({0}) {1};", retval.MarshalType, table.ToNativeReturn (retval.CType, invoke));
			} else
				sw.WriteLine (invoke + ";");

			if (cleanup_str != "")
				sw.Write (cleanup_str);
			sw.WriteLine ("\t\t}");
			sw.WriteLine ();

			sw.WriteLine ("\t\tinternal " + wrapper + " NativeDelegate;");
			sw.WriteLine ("\t\t" + NS + "." + Name + " managed;");
			sw.WriteLine ();

			sw.WriteLine ("\t\tpublic " + Name + "Wrapper (" + NS + "." + Name + " managed)");
			sw.WriteLine ("\t\t{");

			sw.WriteLine ("\t\t\tNativeDelegate = new " + wrapper + " (NativeCallback);");
			sw.WriteLine ("\t\t\tthis.managed = managed;");
			sw.WriteLine ("\t\t}");
			sw.WriteLine ("\t}");
			sw.WriteLine ("#endregion");
			sw.WriteLine ("}");
			sw.Close ();
			return ns + "Sharp." + Name + "Wrapper";
		}
		
		public override void Generate (GenerationInfo gen_info)
		{
			if (!retval.Validate ()) {
				Console.WriteLine("rettype: " + retval.CType + " in callback " + CName);
				Statistics.ThrottledCount++;
				return;
			}

			if (!parms.Validate ()) {
				Console.WriteLine(" in callback " + CName + " **** Stubbing it out ****");
				Statistics.ThrottledCount++;
			}

			sig = new Signature (parms);

			StreamWriter sw = gen_info.OpenStream (Name);

			sw.WriteLine ("namespace " + NS + " {");
			sw.WriteLine ();
			sw.WriteLine ("\tusing System;");
			sw.WriteLine ();
			sw.WriteLine ("\tpublic delegate " + retval.CSType + " " + Name + "(" + sig.ToString() + ");");
			sw.WriteLine ();
			sw.WriteLine ("}");

			sw.Close ();
			
			GenWrapper (NS, gen_info);

			Statistics.CBCount++;
		}
	}
}

