/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using log4net;

namespace DOL.GS.Scripts
{
		/// <summary>
		/// The class for multi language support
		/// </summary>
		public class LngManager
		{
			private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
			/// <summary>
			/// Hashtable of all sentences
			/// </summary>
			protected HybridDictionary m_languageArray;
			
			/// <summary>
			/// Creates a new langage manager
			/// </summary>
			public LngManager()
			{					
			}

			/// <summary>
			/// Start the langage manager
			/// </summary>
			public bool StartLngManager(string lngFileName)
			{
				if (! File.Exists(lngFileName)) return false;

				using (StreamReader filereader = new StreamReader(lngFileName, System.Text.Encoding.Default))
				{
					m_languageArray = new HybridDictionary();

					string currentLine;
					int currentLineNumber = 0;
					int currentKeyNumber = 0;
					
					while ((currentLine = filereader.ReadLine()) != null)
					{
						if (currentLine.Length > 0 && currentLine.StartsWith("["))
						{
							int lineindex = currentLine.IndexOf(']');
							if (lineindex == -1)
							{
								if (log.IsErrorEnabled)
									log.Error("Incorrect file syntax at line : "+currentLineNumber);
								return false;
							}
							string key = currentLine.Substring(1, lineindex - 1);

							lineindex = currentLine.IndexOf('=');
							if (lineindex == -1)
							{
								if (log.IsErrorEnabled)
									log.Warn("Incorrect file syntax at line : "+currentLineNumber);
								return false;	
							}
							string strvalue = currentLine.Substring(lineindex + 1, currentLine.Length - lineindex - 1);
							if (! m_languageArray.Contains(key))
							{
								m_languageArray.Add(key, strvalue);
								currentKeyNumber++;
							}
							else
							{
								if (log.IsWarnEnabled)
									log.Error("Language sentence overrided : (key "+ key +" defined twice)");	
							}
						}
						currentLineNumber++;
					}
					filereader.Close();

					if (log.IsInfoEnabled)
					{
						log.Info("Total key string found in language file : "+ currentKeyNumber);
					}
				}
				return true;
			}

			/// <summary>
			/// Get a string in the good language
			/// </summary>
			public string GetString(string key)
			{
				string str = (string) m_languageArray[key];
				if (str == null)
				{
					if (log.IsErrorEnabled)
						log.Error("Your language file is not complete (missing key : " + key +")");
					return "";
				}
				return str;
			}
		}
}