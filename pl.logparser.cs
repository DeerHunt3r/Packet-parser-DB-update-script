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
using System;
using DOL.GS;
using DOL.Database;
using DOL.GS.PacketHandler;
using System.Collections;
using System.IO;
using System.Reflection;
using log4net;

namespace DOL.GS.Scripts
{
	public enum ePacketType : byte
	{
		Unknown = 0,
		Incoming = 1,
		Outcoming = 2,
	}


	public class LogParser
	{
		StreamReader filereader;

		/// <summary>
		/// Creates a new .log parser
		/// </summary>
		public LogParser()
		{					
		}	

		/// <summary>
		/// Start the .log parser
		/// </summary>
		public bool StartLogParser(string filename)
		{
			if (File.Exists(filename))
			{
				filereader = File.OpenText(filename);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Stop the .log parser
		/// </summary>
		public void CloseLogParser()
		{
			filereader.Close();
		}

		/// <summary>
		/// Get the packet body
		/// </summary>
		public GSPacketIn GetNextPacket()
		{
			return GetNextPacketWithID(ePacketType.Unknown, 0);
		}

		/// <summary>
		/// Get the packet body
		/// </summary>
		public GSPacketIn GetNextIncomingPacket()
		{
			return GetNextPacketWithID(ePacketType.Incoming, 0);
		}

		/// <summary>
		/// Get the packet body
		/// </summary>
		public GSPacketIn GetNextIncomingPacketWithId(byte packetcode)
		{
			return GetNextPacketWithID(ePacketType.Incoming, packetcode);
		}

		/// <summary>
		/// Get the packet body
		/// </summary>
		public GSPacketIn GetNextOutcomingPacket()
		{
			return GetNextPacketWithID(ePacketType.Outcoming, 0);
		}

		/// <summary>
		/// Get the packet body
		/// </summary>
		public GSPacketIn GetNextOutcomingPacketWithId(byte packetcode)
		{
			return GetNextPacketWithID(ePacketType.Outcoming, packetcode);
		}

		/// <summary>
		/// Get the packet body with specified code
		/// </summary>
		/// <param name="packetType">0 = all ; 1 = incoming ; 2 = outcoming</param>
		/// <param name="packetcode">0 = all ; Id of the packet</param>
		/// <returns>the packet if found or null if not</returns>
		private GSPacketIn GetNextPacketWithID(ePacketType packetType, byte packetcode)
		{
			string line;
			ePacketType currentPacketType;
			while ((line = filereader.ReadLine()) != null)
			{
				if(line.StartsWith("<RECV"))
				{
					currentPacketType = ePacketType.Incoming;
				}
				else if (line.StartsWith("<SEND"))
				{
					currentPacketType = ePacketType.Outcoming;
				}
				else
				{
					continue;
				}

				if (packetType == ePacketType.Unknown || currentPacketType == packetType )
				{
					byte packetCode = Convert.ToByte(line.Substring(line.IndexOf("Code:0x")+7, 4), 16);

					if(packetCode == packetcode || packetcode == 0)
					{
						int lenghtStartIndex = line.IndexOf("Len:")+5;
						short packetLenght = Convert.ToInt16(line.Substring(lenghtStartIndex, line.IndexOf('>')-lenghtStartIndex),10);
	
						ArrayList packet = new ArrayList(packetLenght);
						packet.Add((byte)currentPacketType);
						packet.Add(packetCode);
						packet.Add((byte)(packetLenght >> 8));
						packet.Add((byte)(packetLenght & 0xff));
		
						int numberOfLine = (packetLenght%16 == 0) ? packetLenght/16 : packetLenght/16+1;

						for(int i=0 ; i < numberOfLine ;i++)
						{
							line = filereader.ReadLine();
							line = line.Substring(0 , line.IndexOf("  "));
							string[] values = line.Split(' ');
							foreach(string str in values)
							{
								packet.Add(Convert.ToByte(str,16));
							}	
						}
						return (new GSPacketIn(packet.ToArray(typeof(byte)).Length));
					}
				}
			}
			
			CloseLogParser();
			return null;
		}
	}
}
	