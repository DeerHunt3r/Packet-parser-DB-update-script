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
using DOL.GS.Commands;
using DOL.GS.Movement;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using System.IO;
using System.Reflection;
using log4net;

namespace DOL.GS.Scripts
{
	[CmdAttribute(
	  "&parselog",
	  ePrivLevel.Admin,
	  "Parse a log file to complete your actual db",
	  "/parselog <filename>")]
    public class ParseLogCommandHandler : ICommandHandler
	{
//-------------------------------  CONFIGURATION ------------------------------------//
		
		// Select the directory where your files are
		// default from dol directory is : \scripts\log
		static string baseDirectory= GameServer.Instance.Configuration.RootDirectory +Path.DirectorySeparatorChar+ "scripts" +Path.DirectorySeparatorChar+ "log" +Path.DirectorySeparatorChar;

		// Select the language file you want to use
		// default file is : english.lang
		static string languageFileName = "pl.english.lang";

		// Select the output file you want to use to log missing spells
		// default file is : MissingSpells.txt
		static string outputFileName = "MissingSpells.txt";

		// Select the thread priority you want
		// default is :  ThreadPriority.Normal
		static ThreadPriority myThreadPriority = ThreadPriority.Normal;

//-----------------------------  END CONFIGURATION ----------------------------------//

		
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private LogParser parser;
		private LngManager language;  
		private string fileName;
		private string lngFileName;

		public void OnCommand(GameClient client, string[] args)
		{
			if(args.Length != 2)
			{
				  client.Out.SendMessage("Usage: /parselog <filename>",
					eChatType.CT_System,
					eChatLoc.CL_SystemWindow);
				return;
			}

			lngFileName = baseDirectory + languageFileName;

			if (! File.Exists(lngFileName))
			{
				client.Out.SendMessage("The language file "+ languageFileName +" does not exist in "+ baseDirectory,eChatType.CT_System,eChatLoc.CL_SystemWindow);
				return;
			}

			fileName = baseDirectory + args[1] + ".log";

			if (! File.Exists(fileName))
			{
				client.Out.SendMessage("The file "+ args[1] +".log does not exist in "+ baseDirectory,eChatType.CT_System,eChatLoc.CL_SystemWindow);
				return;
			}

			Thread m_ParserThread = new Thread(new ThreadStart(ParseThreadStart));
			m_ParserThread.Priority = myThreadPriority;
			m_ParserThread.Start();
			client.Out.SendMessage("Starting parser thread (see your server console for working progress)...",eChatType.CT_System,eChatLoc.CL_SystemWindow);
			
			return;
		}

		#region Parse thread body
		/// <summary>
		/// Thread start
		/// </summary>
		private void ParseThreadStart()
		{
			int startTime = Environment.TickCount;
			int clientVersion = 0;

			if (log.IsWarnEnabled)
				log.Warn("Starting multi language support ...");
				
			language = new LngManager();
			if(! language.StartLngManager(lngFileName))
			{
				if (log.IsErrorEnabled)
				{
					log.Error("Language managed initialisation failed !!!");
					log.Error("File parsing stoped.");
				}
				return ;
			}
			
			if (log.IsWarnEnabled)
				log.Warn("Multi language support started.");
			
			
			parser = new LogParser();
			if(! parser.StartLogParser(fileName))
			{
				if (log.IsErrorEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			if (log.IsWarnEnabled)
				log.Warn("Begin client version research ...");

			if(! findClientVersion(ref clientVersion))
			{
				if (log.IsErrorEnabled)
				{
					log.Error("Client version not found in file !!!");
					log.Error("File parsing stoped.");
				}
				parser.CloseLogParser();
				return;	
			}

			if(clientVersion < 171)
			{
				if (log.IsErrorEnabled)
				{
					log.Error("Client version found ("+ clientVersion / 100 +"."+ clientVersion % 100 +") is older than 1.71 !!!");
					log.Error("File parsing stoped.");
				}
				parser.CloseLogParser();
				return;
			}
			
			if (log.IsInfoEnabled)
			{
				log.Info("Client version found : "+ clientVersion / 100 +"."+ clientVersion % 100);
			}

			if (log.IsWarnEnabled)
				log.Warn("Client version research ended.");

			if (log.IsWarnEnabled)
				log.Warn("Begin mob research ...");
			
			int mobCount = 0;
			int inventoryCount = 0;
			int itemsCount = 0;

			parseLogFileForMobAndEquipment(clientVersion, ref mobCount, ref inventoryCount, ref itemsCount);

			if (log.IsInfoEnabled)
			{
				log.Info("Mob found = "+mobCount);
				log.Info("Inventory found = "+inventoryCount+" (total items = "+itemsCount+")");
			}

			if (log.IsWarnEnabled)
				log.Warn("Mob research ended.");

			if(! parser.StartLogParser(fileName))
			{
				if (log.IsInfoEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			int merchantCount = 0;
			int tradeItemsListCount = 0;
			int foundItemsCount = 0;
			int parsedItemsCount = 0;
			int createdItemsCount = 0;

			if (log.IsWarnEnabled)
				log.Warn("Begin merchants research ...");
			
			parseLogFileForMerchant(clientVersion, ref merchantCount, ref tradeItemsListCount, ref parsedItemsCount, ref foundItemsCount, ref createdItemsCount);

			if (log.IsInfoEnabled)
			{
				log.Info("Merchants found = "+merchantCount);
				log.Info("Merchants items list found = "+tradeItemsListCount+" , total parsed items = "+parsedItemsCount+" ( items found in db = "+foundItemsCount+" ; items created = "+createdItemsCount+")");
			}

			if (log.IsWarnEnabled)
				log.Warn("Merchants research ended.");
			
			if(! parser.StartLogParser(fileName))
			{
				if (log.IsErrorEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			int staticItemsCount = 0;

			if (log.IsWarnEnabled)
				log.Warn("Begin world objects research ...");
			
			parseLogFileForWorldObjects(clientVersion, ref staticItemsCount);

			if (log.IsInfoEnabled)
				log.Info("World objects found = "+staticItemsCount);

			if (log.IsWarnEnabled)
				log.Warn("World object research ended.");

			if(! parser.StartLogParser(fileName))
			{
				if (log.IsErrorEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			int zonePointsCount = 0;

			if (log.IsWarnEnabled)
				log.Warn("Begin zone points research ...");
			
			parseLogFileForZonePoints(clientVersion, ref zonePointsCount);

			if (log.IsInfoEnabled)
				log.Info("Zone points found = "+zonePointsCount);

			if (log.IsWarnEnabled)
				log.Warn("Zone points research ended.");

			if(! parser.StartLogParser(fileName))
			{
				if (log.IsErrorEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			int bindPointsCount = 0;

			if (log.IsWarnEnabled)
				log.Warn("Begin bind points research ...");
			
			parseLogFileForBindPoints(clientVersion, ref bindPointsCount);

			if (log.IsInfoEnabled)
				log.Info("Bind points found = "+bindPointsCount);

			if (log.IsWarnEnabled)
				log.Warn("Bind points research ended.");

			if(! parser.StartLogParser(fileName))
			{
				if (log.IsErrorEnabled)
					log.Error("File "+ fileName +" not found !!!");
				return ;
			}

			int deletedMobCount = 0;
			int deletedItemsCount = 0;

			if (log.IsWarnEnabled)
				log.Warn("Begin db cleaning ...");
			
			parseLogFileAndCleanDb(clientVersion, ref deletedMobCount, ref deletedItemsCount);

			if (log.IsInfoEnabled)
			{
				log.Info("Total mob deleted = "+deletedMobCount);
				log.Info("Total world objects deleted = "+deletedItemsCount);
			}

			if (log.IsWarnEnabled)
				log.Warn("Db cleaning ended.");

			parser.CloseLogParser();

			if (log.IsWarnEnabled)
			{
				log.Info("File correctly parsed in "+ (Environment.TickCount - startTime) +"ms.");
				log.Warn("Restart the server to refresh your database.");
			}
		
		}
		#endregion
	
		#region Client Version Research parse function
		/// <summary>
		/// Parse the .log file and extract the client version
		/// </summary>
		private bool findClientVersion(ref int clientVersion)
		{
			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			GSPacketIn currentPacket = parser.GetNextOutcomingPacketWithId(0xF4);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // don't use the packet lenght

				switch(currentPacketType)
				{
					case (byte)ePacketType.Outcoming : // traitement de tous les paquets sortants
					{
						switch(currentPacketId)
						{
							case 0xF4 : //Login request
							{
								#region Auto detect client version
								currentPacket.Skip(2);
								clientVersion = (int)currentPacket.ReadByte() * 100;
								clientVersion += (int)currentPacket.ReadByte() * 10;
								clientVersion += (int)currentPacket.ReadByte();
								currentPacket.Skip(90);

								return true;
								#endregion
							}
						}

					}
						break;
				}
				currentPacket = parser.GetNextPacket();
			}
			return false;
		}
		#endregion

		#region BindPoint parse function
		/// <summary>
		/// Parse the .log file, extract all ZonePoints jumps and add it to bd if necessary
		/// </summary>
		private void parseLogFileForBindPoints(int version, ref int bindPointCount)
		{
			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for bind points ...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			int currentXPos = 0;
			int currentYPos = 0;
			int currentZPos = 0;
			ushort currentParsedRegionId = 0;
			eRealm currentRealm = eRealm.None;

			ushort playerObjectId = 0;
		
			GSPacketIn currentPacket = parser.GetNextOutcomingPacketWithId(0xFC);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // i don't use the packet lenght

				switch(currentPacketType)
				{
					case (byte)ePacketType.Outcoming :  // traitement de tous les paquets sortants
					{
						switch(currentPacketId)
						{
							case 0xFC : // realm selection
							{
								#region Find Current Realm

								string accountName = currentPacket.ReadString(24);
								if(accountName.EndsWith("-S"))      currentRealm = eRealm.Albion;
								else if(accountName.EndsWith("-N")) currentRealm = eRealm.Midgard;
								else if(accountName.EndsWith("-H")) currentRealm = eRealm.Hibernia;

								#endregion
							}
								break;

							case 0xA9 : // player move
							{
								#region Get current player position

								currentPacket.Skip(4);
								currentZPos = (int) currentPacket.ReadShort();
								currentXPos = (int) currentPacket.ReadShort();
								currentYPos = (int) currentPacket.ReadShort();
								ushort currentZoneID = (ushort) currentPacket.ReadByte();

								Zone newZone = WorldMgr.GetZone(currentZoneID);
								if(newZone != null)
								{
									currentXPos = newZone.XOffset + currentXPos;
									currentYPos = newZone.YOffset + currentYPos;
								}

								#endregion
							}
								break;
						}
					}
						break;

					case (byte)ePacketType.Incoming :  // traitement de tous les paquets entrants
					{
						switch(currentPacketId)
						{
							case 0x20 : // player position and object ID
							{
								#region Update player pos and oid

								playerObjectId = currentPacket.ReadShort();
								currentZPos = (int)currentPacket.ReadShort();
								currentXPos = (int)currentPacket.ReadInt();
								currentYPos = (int)currentPacket.ReadInt(); 
								currentPacket.Skip(2);
								currentPacket.Skip(6);
								currentParsedRegionId = currentPacket.ReadShort();
								currentPacket.Skip(2);
			
								#endregion
							}
								break;

							case 0xF9 : // Send emote
							{
								#region Save bind point

								if(currentPacket.ReadShort() == playerObjectId)
								{
									if( currentPacket.ReadByte() == 0x2C) //bind emote
									{
										BindPoint currentBindPoint = GameServer.Database.SelectObject<BindPoint>("X = '"+currentXPos+"' AND Y = '"+currentYPos+"' AND Z = '"+currentZPos+"' AND Region = '"+currentParsedRegionId+"' AND Realm = '"+(int)currentRealm+"'");
										if(currentBindPoint == null)
										{
											currentBindPoint = new BindPoint();
											currentBindPoint.X = currentXPos;
											currentBindPoint.Y = currentYPos;
											currentBindPoint.Z = currentZPos;
											currentBindPoint.Region = currentParsedRegionId;
											currentBindPoint.Realm = (int)currentRealm;
											currentBindPoint.Radius = 750;

											GameServer.Database.AddObject(currentBindPoint);
										}
										bindPointCount++;
									}
								}
								#endregion
							}
								break;
						}
					}
						break;
				}
				
				currentPacket = parser.GetNextPacket();
			}
			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
		#endregion

		#region ZonePoint parse function
		/// <summary>
		/// Parse the .log file, extract all ZonePoints jumps and add it to bd if necessary
		/// </summary>
		private void parseLogFileForZonePoints(int version, ref int zonePointCount)
		{
			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for zone points ...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			eRealm currentRealm = eRealm.None;

			// current parsed info
			ZonePoint currentZonePoint = null;

			GSPacketIn currentPacket = parser.GetNextOutcomingPacketWithId(0xFC);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // i don't use the packet lenght

				switch(currentPacketType)
				{
					case (byte)ePacketType.Outcoming :  // traitement de tous les paquets sortants
					{
						switch(currentPacketId)
						{
							case 0x90 : //teleport jump request
							{
								#region Set zone point id

								ushort zonePointId = currentPacket.ReadShort();
								currentPacket.Skip(2);

								currentZonePoint = GameServer.Database.SelectObject<ZonePoint>("Id = '"+zonePointId+"' AND Realm = '"+(int)currentRealm+"'");	
								if(currentZonePoint == null)
								{
									currentZonePoint = new ZonePoint();
									currentZonePoint.Id = zonePointId;
									currentZonePoint.Realm = (ushort)currentRealm;
								}

								#endregion
							}
								break;

							case 0xFC : // realm selection
							{
								#region Find Current Realm

								string accountName = currentPacket.ReadString(24);
								if(accountName.EndsWith("-S"))      currentRealm = eRealm.Albion;
								else if(accountName.EndsWith("-N")) currentRealm = eRealm.Midgard;
								else if(accountName.EndsWith("-H")) currentRealm = eRealm.Hibernia;

								#endregion
							}
								break;
						}
					}
						break;

					case (byte)ePacketType.Incoming :  // traitement de tous les paquets entrants
					{
						switch(currentPacketId)
						{
							case 0x20 : // Set Player position and object id
							{
								#region Decode and save jump point

								if(currentZonePoint != null)
								{
									currentPacket.Skip(2); // This packet is used to find what region is in log
									currentZonePoint.TargetZ = (int)currentPacket.ReadShort();
									currentZonePoint.TargetX = (int)currentPacket.ReadInt();
									currentZonePoint.TargetY = (int)currentPacket.ReadInt(); 

									currentZonePoint.TargetHeading = currentPacket.ReadShort();
									currentPacket.Skip(6);
									currentZonePoint.TargetRegion = currentPacket.ReadShort();
									currentPacket.Skip(2);

									/*if(IsDungeon(currentZonePoint.Region))
									{
										currentZonePoint.X = currentZonePoint.X & 0xFFFF;
										currentZonePoint.Y = currentZonePoint.Y & 0xFFFF;
									}*/

									ZonePoint checkZonePoint = GameServer.Database.SelectObject<ZonePoint>("ZonePoint_ID = '"+currentZonePoint.ObjectId+"'");
									if(checkZonePoint != null)
									{
										GameServer.Database.SaveObject(currentZonePoint);
									}
									else
									{
										GameServer.Database.AddObject(currentZonePoint);
									}

									zonePointCount++;
								}

								#endregion
							}
								break;
						}
					}
						break;
				}
				
				currentPacket = parser.GetNextPacket();
			}
			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
		#endregion

		#region World Object parse function
		/// <summary>
		/// Parse the .log file, extract all world objects and add it to bd if necessary
		/// </summary>
		private void parseLogFileForWorldObjects(int version, ref int itemsCount)
		{
			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for world objects ...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			ushort currentParsedRegionId = 0;

			GSPacketIn currentPacket = parser.GetNextIncomingPacketWithId(0x20);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // don't use the packet lenght
	
				switch(currentPacketId)
				{
					case 0x20 : // Set Player position and object id
					{
						#region Find current Region Id
						currentPacket.Skip(20); // This packet is used to find what region is in log
						currentParsedRegionId = currentPacket.ReadShort();
						#endregion
					}
						break;

					case 0xD9 : // Object Creation
					{
						#region Decode and save world object

						currentPacket.Skip(2); // internal object id
						ushort emblem = currentPacket.ReadShort();
						ushort heading = currentPacket.ReadShort();
						int z = (int)currentPacket.ReadShort();
						int x = (int)currentPacket.ReadInt();
						int y = (int)currentPacket.ReadInt();

						/*if(IsDungeon(currentParsedRegionId))
						{
							currentPacket.Skip(2); //x and y on 2 byte for dungeon
							x = (int)currentPacket.ReadShort();
							currentPacket.Skip(2);
							y = (int)currentPacket.ReadShort();
						}
						else
						{
							x = (int)currentPacket.ReadInt();
							y = (int)currentPacket.ReadInt();
						}*/

						ushort model = currentPacket.ReadShort();
						ushort objectType = currentPacket.ReadShort();
						currentPacket.Skip(4);
						string name = currentPacket.ReadPascalString();

						if(currentPacket.ReadByte() == 0x04) // TODO door
						{
							break;
						}

						itemsCount++;

						#region Save World Object

						WorldObject currentObject = GameServer.Database.SelectObject<WorldObject>("Region = '"+currentParsedRegionId+"' AND X = '"+x+"' AND Y = '"+y+"' AND Z = '"+z+"' AND Heading = '"+heading+"'");
						if(currentObject == null)
						{
							currentObject = new WorldObject();
							currentObject.ClassType = "DOL.GS.GameStaticItem";
							currentObject.Name = name;
							currentObject.X = x;
							currentObject.Y = y;
							currentObject.Z = z;
							currentObject.Heading = heading;
							currentObject.Region = currentParsedRegionId;
							currentObject.Model = model;
							currentObject.Emblem = emblem;
						
							GameServer.Database.AddObject(currentObject);
						}

						#endregion

						#endregion
					}
						break;
				}
				
				currentPacket = parser.GetNextIncomingPacket();
			}

			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
		#endregion

		#region Merchant / Stable Master parse function
		/// <summary>
		/// Parse the .log file, extract all merchants and add it to bd if necessary
		/// </summary>
		private void parseLogFileForMerchant(int version, ref int merchantCount,ref int tradeItemsListCount,ref int parsedItemsCount,ref int foundItemsCount,ref int createdItemsCount)
		{
			StreamWriter sw = new StreamWriter(File.Create(baseDirectory + outputFileName));

			Hashtable newMerchantItemsList = precachingActualMerchantItemsDb();

			Hashtable allFoundNpcIdByRegion = new Hashtable(); //(regionid => ( npcgameid => Mob) constrain only merchant and stable master)

			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for merchant ...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			eRealm currentRealm = eRealm.None;
			ushort currentParsedRegionId = 0;
			ushort currentMerchantId = 0;
			DBMerchantTradeItems currentMerchantItems = null;

			GSPacketIn currentPacket = parser.GetNextOutcomingPacketWithId(0xFC);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // don't use the packet lenght

				switch(currentPacketType)
				{
					case (byte)ePacketType.Incoming :  // traitement de tous les paquets entrants
					{
						switch(currentPacketId)
						{
							case 0x20 : // Set Player position and object id
							{
								#region Find current Region Id
								currentPacket.Skip(20); // This packet is used to find what region is in log
								currentParsedRegionId = currentPacket.ReadShort();

								if(! allFoundNpcIdByRegion.Contains(currentParsedRegionId))
								{
									allFoundNpcIdByRegion.Add(currentParsedRegionId, new Hashtable());
								}
								#endregion
							}
								break;

							case 0xDA : //npc create
							{
								# region Find all merchants

								ushort currentMobId = currentPacket.ReadShort();
								
								Hashtable allFoundMerchants = (Hashtable)allFoundNpcIdByRegion[currentParsedRegionId];

								currentPacket.Skip(4);
								int z = (int)currentPacket.ReadShort();

								int x = (int)currentPacket.ReadInt();
								int y = (int)currentPacket.ReadInt();

								/*if(IsDungeon(currentParsedRegionId))
								{
									currentPacket.Skip(2); //x and y on 2 byte for dungeon
									x = (int)currentPacket.ReadShort();
									currentPacket.Skip(2);
									y = (int)currentPacket.ReadShort();
								}
								else
								{
									x = (int)currentPacket.ReadInt();
									y = (int)currentPacket.ReadInt();
								}*/
								
								currentPacket.Skip(6);
								byte realm = (byte) ((currentPacket.ReadByte() & 0xC0) >>6);
								currentPacket.Skip(1);
								currentPacket.Skip(4); //unknown new in 1.71
								string name = currentPacket.ReadPascalString();
								string guildName = currentPacket.ReadPascalString();
								currentPacket.Skip(1);

								if(guildName.IndexOf(language.GetString("merchant_string")) >= 0 || guildName.IndexOf(language.GetString("stable_master_string")) >= 0)
								{
									Mob currentNpc = (Mob) GameServer.Database.SelectObject<Mob>("Name = '"+ GameServer.Database.Escape(name) +"' AND Region = '"+currentParsedRegionId+"' AND Realm = '"+realm+"' AND X = '"+x+"' AND Y = '"+y+"' AND Z = '"+z+"'");
									if(currentNpc != null && ! allFoundMerchants.Contains(currentMobId))
									{
										currentNpc.Guild = guildName;
										allFoundMerchants.Add(currentMobId ,currentNpc);
										merchantCount++;
									}
								}
								#endregion
							}
								break;
							case 0xC4 : // delve info
							{
								#region Delve Info

								if(currentMerchantItems != null)
								{
									string itemName = currentPacket.ReadPascalString();
									if(itemName != null)
									{
										ItemTemplate currentItem = currentMerchantItems.GetItemByName(itemName);
										if(currentItem != null)
										{

											while(currentPacket.ReadByte() != 0)
											{
												string currentLine = currentPacket.ReadPascalString();
												if(currentLine.Equals(" ")) continue;

												if(currentLine.IndexOf(language.GetString("usable_by_string")) >= 0)
												{
													#region decode Usable By
													// decode usable by
													currentPacket.Skip(1);
													while((currentLine = currentPacket.ReadPascalString()) != " ")
													{
														// decode Usable By when in db
														currentPacket.Skip(1);
													}
													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("shield_delve_string")) >= 0)
												{
													#region decode Shield Delve
													// decode shield values
													currentPacket.Skip(1);
													while((currentLine = currentPacket.ReadPascalString()) != " ")
													{
														if(currentItem.Object_Type == (int)eObjectType.Shield)
														{
															currentLine = currentLine.Substring(2); // cut the "- "
															string currentStrValue = currentLine.Substring(0, currentLine.IndexOf(" ")).Replace(".", ","); //extract number

															int currentValue = (int) (Convert.ToDouble(currentStrValue) * 10);
															if(currentLine.IndexOf(language.GetString("shield_dps_string")) >= 0)
															{
																currentItem.DPS_AF = currentValue;
															}
															else if(currentLine.IndexOf(language.GetString("shield_spd_string")) >= 0)
															{
																currentItem.SPD_ABS = currentValue;
															}
														}
														currentPacket.Skip(1);
													}

													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("focus_bonuses_string")) >= 0)
												{
													#region decode Focus bonus
													// decode focus bonus
													currentPacket.Skip(1);
													byte bonusNumber=0;
													while((currentLine = currentPacket.ReadPascalString()) != " ")
													{
														// decode
														currentLine = currentLine.Substring(2); // cut the "- "

														int indexOfHalf = currentLine.IndexOf(":");

														switch(bonusNumber)
														{
															case 0: currentItem.Bonus1 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.Bonus1Type = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
															case 1: currentItem.Bonus2 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.Bonus2Type = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
															case 2: currentItem.Bonus3 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.Bonus3Type = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
															case 3: currentItem.Bonus4 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.Bonus4Type = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
															case 4: currentItem.Bonus5 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.Bonus5Type = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
															case 5: currentItem.ExtraBonus = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf(language.GetString("lvls_string"))-indexOfHalf-3));
																currentItem.ExtraBonusType = FocusNameToPropriety(currentLine.Substring(0, indexOfHalf));
																break;
														}
														bonusNumber++;
			
														currentPacket.Skip(1);
													}

													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("magical_bonuses_string")) >= 0)
												{
													#region decode Magical Bonus
													// decode magic bonus
													currentPacket.Skip(1);
													byte bonusNumber=0;
													while((currentLine = currentPacket.ReadPascalString()) != " ")
													{
														// decode
														currentLine = currentLine.Substring(2); // cut the "- "

														int indexOfHalf = currentLine.IndexOf(":");
														bool isResistBonus = (currentLine.IndexOf("pts") >= 0) ? false : true ;

														switch(bonusNumber)
														{
															case 0: if(isResistBonus)
																	{
																		currentItem.Bonus1 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.Bonus1Type = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.Bonus1 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.Bonus1Type = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
															case 1: if(isResistBonus)
																	{
																		currentItem.Bonus2 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.Bonus2Type = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.Bonus2 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.Bonus2Type = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
															case 2: if(isResistBonus)
																	{
																		currentItem.Bonus3 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.Bonus3Type = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.Bonus3 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.Bonus3Type = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
															case 3: if(isResistBonus)
																	{
																		currentItem.Bonus4 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.Bonus4Type = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.Bonus4 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.Bonus4Type = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
															case 4: if(isResistBonus)
																	{
																		currentItem.Bonus5 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.Bonus5Type = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.Bonus5 = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.Bonus5Type = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
															case 5: if(isResistBonus)
																	{
																		currentItem.ExtraBonus = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2));
																		currentItem.ExtraBonusType = ResistNameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																	else
																	{
																		currentItem.ExtraBonus = Convert.ToInt32(currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("pts")-indexOfHalf-3));
																		currentItem.ExtraBonusType = NameToPropriety(currentLine.Substring(0, indexOfHalf));
																	}
																break;
														}
														bonusNumber++;

														currentPacket.Skip(1);
													}

													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("quality_string")) >= 0)
												{
													#region decode Quality
													// correct quality % with delve
													currentLine = currentLine.Substring(2); // cut the "- "
													currentItem.Quality = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("%")));

													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("weapon_speed_string")) >= 0)
												{
													#region decode Weapon Speed
													// get speed with delve for thrown weapon
													if(currentItem.Object_Type == (int)eObjectType.Thrown)
													{
														currentLine = currentLine.Substring(2); // cut the "- "
														string currentStrValue = currentLine.Substring(0, currentLine.IndexOf(" ")).Replace(".", ","); //extract number
														
														currentItem.SPD_ABS = (int) (Convert.ToDouble(currentStrValue) * 10);
													}
													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("level_requirement_string")) >= 0) // all items with a spell
												{
													#region decode All proc / charges items / poison / potions spells

													int spellLevel = 0;
													string delveSpellType = null; // only for save it if not found
													string dolSpellType = null;
													int damage = 0;
													int duration = 0;
													int valeur = 0;
													int radius = 0;
													int damageType = 10;

													currentPacket.Skip(1);
													while((currentLine = currentPacket.ReadPascalString()) != " ")
													{
														currentLine = currentLine.Substring(2); // cut the "- "
														spellLevel = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf(" "))); //extract the spell level

														currentPacket.Skip(1);
													}

													currentPacket.Skip(1);
													while((currentLine = currentPacket.ReadPascalString()) == " ") // skip all write lines
													{
														currentPacket.Skip(1);
													}

													bool isChargedItem = false;

													#region Decode poison / charged items specifics infos
													if(currentLine.IndexOf(language.GetString("poison_string")) >= 0 || currentLine.IndexOf(language.GetString("charged_magic_string")) >= 0 || currentLine.IndexOf(language.GetString("poison_magic_string")) >= 0) // It's a Poison or charged item
													{
														isChargedItem = true;

														if(currentLine.IndexOf(language.GetString("poison_string")) >= 0)  // skip the poison level and go to the Offensive Proc Ability: line
														{
															currentPacket.Skip(1);
															while(currentPacket.ReadPascalString() != language.GetString("poison_magic_string"))
															{
																currentPacket.Skip(1);
															}
														}

														currentPacket.Skip(1);
														while((currentLine = currentPacket.ReadPascalString()) != " ")
														{
															// decode	
															if(currentLine.IndexOf(language.GetString("charge_string")) >= 0) // decode charge
															{
																currentLine = currentLine.Substring(2); // cut the "- "
																string currentStrCharges = currentLine.Substring(0, currentLine.IndexOf(" ")); //extract number
																int currentCharges = Convert.ToInt32(currentStrCharges);
																currentItem.Charges = currentCharges;
															}
															else if(currentLine.IndexOf(language.GetString("max_charge_string")) >= 0) // decode max charge
															{
																currentLine = currentLine.Substring(2); // cut the "- "
																string currentStrCharges = currentLine.Substring(0, currentLine.IndexOf(" "));
																int currentCharges = Convert.ToInt32(currentStrCharges);
																currentItem.MaxCharges = currentCharges;
															}
															else if(currentLine.IndexOf(language.GetString("function_string")) >= 0) // find spell type
															{
																int indexOfHalf = currentLine.IndexOf(":");
																delveSpellType = currentLine.Substring(indexOfHalf+2);
															}
															currentPacket.Skip(1);
														}
													}
													#endregion

													#region Decode proc / reactive proc specific infos
													else if(currentLine.IndexOf(language.GetString("magical_ability_string")) >= 0) // Proc / reactive proc
													{
														currentPacket.Skip(1);
														while((currentLine = currentPacket.ReadPascalString()) != " ")
														{
															if(currentLine.IndexOf(language.GetString("function_string")) >= 0) // find spell type
															{
																int indexOfHalf = currentLine.IndexOf(":");
																delveSpellType = currentLine.Substring(indexOfHalf+2);
															}
															currentPacket.Skip(1);
														}
													}
													#endregion

													if((dolSpellType = SpellTypeToDolSpellType(delveSpellType, currentItem.Object_Type)) != null)
													{
														#region Skip the spell description
														currentPacket.Skip(1);
														while((currentLine = currentPacket.ReadPascalString()) != " ") // skip the spell description
														{
															currentPacket.Skip(1);
														}
														#endregion

														#region Decode all spells specifics infos (damage, duration ect ...)

														if(dolSpellType.Equals(language.GetString("DamageOverTime")))
														{
															#region Damge Over Time Poison

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode damage over time strings
															{
																if(currentLine.IndexOf(language.GetString("damage_per_tick_string")) >= 0)
																{
																	string strDamagePerTick = currentLine.Substring(currentLine.IndexOf(":")+2); //extract number
																	damage = (int)Convert.ToInt32(strDamagePerTick);
																}
																else if(currentLine.IndexOf(language.GetString("duration_string")) >= 0)
																{
																	int min = 0;
																	int sec = 0;

																	currentLine = currentLine.Substring(currentLine.IndexOf(":")+2); // cut the "Duration:"
																	if(currentLine.IndexOf("min") >= 0)
																	{
																		int indexOfHalf = currentLine.IndexOf(":");
																		min = Convert.ToInt32(currentLine.Substring(0, indexOfHalf));
																		sec = Convert.ToInt32(currentLine.Substring(indexOfHalf+1, currentLine.IndexOf("min")-indexOfHalf-2));
																	}
																	else
																	{
																		sec = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("sec")-1));
																	}
																	duration = min * 60 + sec;
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														else if(dolSpellType.Equals(language.GetString("Disease")))
														{
															#region Disease

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode damage over time strings
															{
																if(currentLine.IndexOf(language.GetString("duration_string")) >= 0)
																{
																	int min = 0;
																	int sec = 0;

																	currentLine = currentLine.Substring(currentLine.IndexOf(":")+2); // cut the "Duration:"
																	if(currentLine.IndexOf("min") >= 0)
																	{
																		int indexOfHalf = currentLine.IndexOf(":");
																		min = Convert.ToInt32(currentLine.Substring(0, indexOfHalf));
																		sec = Convert.ToInt32(currentLine.Substring(indexOfHalf+1, currentLine.IndexOf("min")-indexOfHalf-2));
																	}
																	else
																	{
																		sec = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("sec")-1));
																	}
																	duration = min * 60 + sec;
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														else if(dolSpellType.Equals(language.GetString("SpeedDecrease")) || dolSpellType.Equals(language.GetString("UnbreakableSpeedDecrease")))
														{
															#region Speed Decrease

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode speed decrease strings
															{
																if(currentLine.IndexOf(language.GetString("value_string")) >= 0)
																{
																	int indexOfHalf = currentLine.IndexOf(":");
																	string strValue = currentLine.Substring(indexOfHalf+2, currentLine.IndexOf("%")-indexOfHalf-2); //extract number
																	valeur = (int)Convert.ToInt32(strValue);
																}
																else if(currentLine.IndexOf(language.GetString("duration_string")) >= 0) 
																{
																	int min = 0;
																	int sec = 0;

																	currentLine = currentLine.Substring(currentLine.IndexOf(":")+2); // cut the "Duration:"
																	if(currentLine.IndexOf("min") >= 0)
																	{
																		int indexOfHalf = currentLine.IndexOf(":");
																		min = Convert.ToInt32(currentLine.Substring(0, indexOfHalf));
																		sec = Convert.ToInt32(currentLine.Substring(indexOfHalf+1, currentLine.IndexOf("min")-indexOfHalf-2));
																	}
																	else
																	{
																		sec = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("sec")-1));
																	}
																	duration = min * 60 + sec;
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														else if(dolSpellType.Equals(language.GetString("StrengthDebuff")))
														{
															#region Str Decrease

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode str debuff strings
															{
																if(currentLine.IndexOf(language.GetString("strength_penalty_string")) >= 0)
																{
																	string strValue = currentLine.Substring(currentLine.IndexOf(":")+2); //extract number
																	valeur = (int)Convert.ToInt32(strValue);
																}
																else if(currentLine.IndexOf(language.GetString("duration_string")) >= 0) 
																{
																	int min = 0;
																	int sec = 0;

																	currentLine = currentLine.Substring(currentLine.IndexOf(":")+2); // cut the "Duration:"
																	if(currentLine.IndexOf("min") >= 0)
																	{
																		int indexOfHalf = currentLine.IndexOf(":");
																		min = Convert.ToInt32(currentLine.Substring(0, indexOfHalf));
																		sec = Convert.ToInt32(currentLine.Substring(indexOfHalf+1, currentLine.IndexOf("min")-indexOfHalf-2));
																	}
																	else
																	{
																		sec = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("sec")-1));
																	}
																	duration = min * 60 + sec;
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														else if(dolSpellType.Equals(language.GetString("StrengthConstitutionDebuff")))
														{
															#region Str / Con Decrease

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode str con debuff strings
															{
																if(currentLine.IndexOf(language.GetString("str_con_penalty_string")) >= 0)
																{
																	string strValue = currentLine.Substring(currentLine.IndexOf(":")+2); //extract number
																	valeur = (int)Convert.ToInt32(strValue);
																}
																else if(currentLine.IndexOf(language.GetString("duration_string")) >= 0) 
																{
																	int min = 0;
																	int sec = 0;

																	currentLine = currentLine.Substring(currentLine.IndexOf(":")+2); // cut the "Duration:"
																	if(currentLine.IndexOf("min") >= 0)
																	{
																		int indexOfHalf = currentLine.IndexOf(":");
																		min = Convert.ToInt32(currentLine.Substring(0, indexOfHalf));
																		sec = Convert.ToInt32(currentLine.Substring(indexOfHalf+1, currentLine.IndexOf("min")-indexOfHalf-2));
																	}
																	else
																	{
																		sec = Convert.ToInt32(currentLine.Substring(0, currentLine.IndexOf("sec")-1));
																	}
																	duration = min * 60 + sec;
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														else if(dolSpellType.Equals(language.GetString("DirectDamage")))
														{
															#region Direct Damage

															currentPacket.Skip(1);
															while((currentLine = currentPacket.ReadPascalString()) != " ")  // decode direct damage strings
															{
																if(currentLine.IndexOf(language.GetString("radius_string")) >= 0)
																{
																	radius = (int)Convert.ToInt32(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																else if (currentLine.IndexOf(language.GetString("damage_type_string")) >= 0)
																{
																	damageType = DamageTypeNameToPropriety(currentLine.Substring(currentLine.IndexOf(":")+2));
																	if(damageType == 0) damage = Convert.ToInt32(currentLine.Substring(currentLine.IndexOf(":")+2));
																}
																currentPacket.Skip(1);
															}

															#endregion
														}
														
														#endregion

														#region Find the current spell in the db and save it if found

														DataObject[] allSpells = (DataObject[]) GameServer.Database.SelectObjects<DBSpell>("Type = '"+dolSpellType+"' AND Power = 0 AND CastTime = 0 AND DamageType ="+damageType+" AND Damage ="+damage+" AND Duration ="+duration+" AND Value ="+valeur+" AND Radius="+radius);
														foreach(DBSpell spell in allSpells)
														{
															DBLineXSpell isGoodSpellLevel = (DBLineXSpell) GameServer.Database.SelectObject<DBLineXSpell>( "SpellID = '"+spell.SpellID+"' AND Level = "+spellLevel);
															if(isGoodSpellLevel != null)
															{
																if(isChargedItem) currentItem.SpellID = spell.SpellID;
																else currentItem.ProcSpellID = spell.SpellID;
																break;
															}
														}
														
														#endregion

														if(currentItem.SpellID == 0 && currentItem.ProcSpellID == 0)
														{
															if (log.IsWarnEnabled)
															{
																log.Warn("Spell not found, see "+outputFileName+" for more infos");
															}

															sw.WriteLine("-------------------");
															sw.WriteLine("Spell not found");
															sw.WriteLine("Level = "+spellLevel+" ,Type = '"+dolSpellType+"', Power = 0, CastTime = 0, DamageType ="+damageType+", Damage ="+damage+", Duration ="+duration+", Value ="+valeur+", Radius ="+radius);
															sw.WriteLine("");
															sw.Flush();
														}
													}
													else
													{
														if (log.IsWarnEnabled)
														{
															log.Warn("Spell type not found, see "+outputFileName+" for more infos");
														}

														sw.WriteLine("-------------------");
														sw.WriteLine("Spell type not found : "+delveSpellType);
														sw.WriteLine("");
														sw.Flush();
													}

													#endregion
												}
												else if(currentLine.IndexOf(language.GetString("sold_string")) >= 0)
												{
													// set this flag
												}
												else if(currentLine.IndexOf(language.GetString("trade_string")) >= 0)
												{
													// set this flag
												}
												else if(currentLine.IndexOf(language.GetString("siege_ammunition_string")) >= 0)
												{
													// set this flag
												}
											}
											GameServer.Database.SaveObject(currentItem);
										}
									}
								}

								#endregion
							}
								break;

							case 0x17 : // merchant items list
							{
								Hashtable allFoundMerchants = (Hashtable)allFoundNpcIdByRegion[currentParsedRegionId];
								Mob merchant = (Mob) allFoundMerchants[currentMerchantId]; // get back the actual merchant

								if(merchant != null)
								{
									#region Decode merchants items

									currentMerchantItems = new DBMerchantTradeItems(currentMerchantId.ToString()); // create a new empty merchant items list

									while(currentPacketId == 0x17) // for each page there is a new packet
									{
										byte itemsQuantity = (byte)currentPacket.ReadByte(); 
										currentPacket.Skip(1); // windows type
										byte pageNumber = (byte)currentPacket.ReadByte();
										currentPacket.Skip(1); // unused

										for(int i = 0 ; i < itemsQuantity ; i++)
										{
											byte indexOnPage = (byte)currentPacket.ReadByte();
											byte level = (byte)currentPacket.ReadByte();
											byte value1 = (byte)currentPacket.ReadByte();
											byte SPD_ABS = (byte)currentPacket.ReadByte();
											byte temp = (byte)currentPacket.ReadByte();

											byte hand = (byte)(temp >>6);

											temp = (byte)currentPacket.ReadByte();

											byte typeDamage = (byte)(temp >>6);
											byte objectType = (byte)(temp & 0x3F);

											bool canUse = false;
											temp = (byte)currentPacket.ReadByte();
											if(temp == 0) canUse = true;

											ushort value2 = currentPacket.ReadShort();

											int cost = (int)currentPacket.ReadInt();
											ushort model = currentPacket.ReadShort();
											string name = currentPacket.ReadPascalString();

											parsedItemsCount++;

											#region Add merchant item to his list

											if(IsPoison(model)) objectType = 46;

											ItemTemplate currentItem = GameServer.Database.SelectObject<ItemTemplate>("Name = '"+GameServer.Database.Escape(name)+"' AND Realm = '"+(int)currentRealm +"' AND Level = '"+level+"' AND Object_Type = '"+objectType+"' AND Model = '"+model+"' AND Price = '"+cost+"'");
											if( currentItem != null)
											{
												foundItemsCount++;
											}
											else
											{
												currentItem = new ItemTemplate();

												currentItem.Id_nb = name.Replace(" ", "_");
												ItemTemplate item = GameServer.Database.SelectObject<ItemTemplate>("Id_nb = '"+GameServer.Database.Escape(currentItem.Id_nb)+"'");
												if(item != null)
												{
													int number = 1;
													do
													{
														number++;
														item = GameServer.Database.SelectObject<ItemTemplate>("Id_nb = '"+GameServer.Database.Escape(currentItem.Id_nb+number)+"'");
													}
													while (item != null);
													currentItem.Id_nb += number;
												}
												
												currentItem.ObjectId = System.Guid.NewGuid().ToString();
												currentItem.Name = name;
												currentItem.Realm = (int) currentRealm;
												currentItem.Level = level;
												currentItem.MaxDurability = 100;
												currentItem.Durability = 100;
												currentItem.Condition = 50000;
												currentItem.MaxCondition = 50000;
												currentItem.Quality = 85;
												currentItem.SPD_ABS = SPD_ABS;
												currentItem.Type_Damage = typeDamage;
												currentItem.Hand = hand;
												currentItem.Model = model;
												currentItem.Price = (long) cost;
												currentItem.Weight = value2;
												
												currentItem.Object_Type = objectType;
												switch(currentItem.Object_Type)
												{
													case (int) eObjectType.Arrow:
													case (int) eObjectType.Bolt:
													{
														currentItem.PackSize = value1;
														currentItem.Item_Type = 40;
														currentItem.MaxCount = 200;
														break;
													}

													case (int) eObjectType.Thrown:
													{
														currentItem.DPS_AF = value1;
														currentItem.Item_Type = 13;
														currentItem.MaxCount = 100;
														currentItem.PackSize = 20; // set to 20 because wrong on some merchand
														break;
													}

													case (int) eObjectType.GenericItem:
													{
														currentItem.PackSize = value1;
														currentItem.Item_Type = GetItemTypeFromModel(currentItem.Name, currentItem.Model);
														if(IsCraftMaterial(currentItem.Model))
														{
															currentItem.MaxCount = 200;
														}
														else if(IsDye(currentItem.Model))
														{
															currentItem.Color = GetDyeColorFromName(currentItem.Name);
														}
														break;
													}
														
													case (int) eObjectType.Shield:
													{
														currentItem.Type_Damage = value1;
														currentItem.Item_Type = 11;
														break;
													}
														
													case (int) eObjectType.CrushingWeapon:
													case (int) eObjectType.SlashingWeapon:
													case (int) eObjectType.ThrustWeapon:
													case (int) eObjectType.Sword:
													case (int) eObjectType.Hammer:
													case (int) eObjectType.Axe:
													case (int) eObjectType.Blades:
													case (int) eObjectType.Blunt:
													case (int) eObjectType.Piercing:
													case (int) eObjectType.Flexible:
													case (int) eObjectType.HandToHand:
													{
														currentItem.DPS_AF = value1;
														if(currentItem.Hand == 0)
														{
															currentItem.Item_Type = 10;
														}
														else if(currentItem.Hand == 1)
														{
															currentItem.Item_Type = 12;
														}
														else
														{
															currentItem.Item_Type = 11;
														}
														break;
													}
														
													case (int) eObjectType.Fired:
													case (int) eObjectType.Longbow:
													case (int) eObjectType.Crossbow:
													case (int) eObjectType.CompositeBow:
													case (int) eObjectType.RecurvedBow:	
													{
														currentItem.DPS_AF = value1;
														currentItem.Item_Type = 13;
														break;
													}
														
													case (int) eObjectType.TwoHandedWeapon:
													case (int) eObjectType.PolearmWeapon:
													case (int) eObjectType.Staff:
													case (int) eObjectType.Spear:
													case (int) eObjectType.LargeWeapons:
													case (int) eObjectType.CelticSpear:
													case (int) eObjectType.Scythe:
													{
														currentItem.DPS_AF = value1;
														currentItem.Item_Type = 12;
														break;
													}

													case (int) eObjectType.Instrument:
													{
														currentItem.DPS_AF = value1;
														if(currentItem.DPS_AF == 0) currentItem.DPS_AF = 2; // correct lute to work with dol
														currentItem.Item_Type = 12;
														break;
													}
														
													case (int) eObjectType.GenericArmor:
													case (int) eObjectType.Cloth:
													case (int) eObjectType.Leather:
													case (int) eObjectType.Studded:
													case (int) eObjectType.Chain:
													case (int) eObjectType.Plate:
													case (int) eObjectType.Reinforced:
													case (int) eObjectType.Scale:
													{
														currentItem.DPS_AF = value1;
														currentItem.Item_Type = GetArmorTypeFromModel(currentItem.Name, currentItem.Model);
														break;
													}
													

													case (int) eObjectType.Magical:
													{
														currentItem.DPS_AF = value1;
														currentItem.Item_Type = GetItemTypeFromModel(currentItem.Name, currentItem.Model);

														if(IsPoison(currentItem.Model))
														{
															currentItem.Object_Type = 46;
															currentItem.MaxCount = 100;
															currentItem.PackSize = 20;
														}
														break;
													}

													case (int) eObjectType.Poison:
													{
														currentItem.PackSize = value1;
														currentItem.Item_Type = 40;
														currentItem.MaxCount = 100;
														break;
													}
												}

												if(currentItem.PackSize == 0) currentItem.PackSize = 1;
												currentItem.Weight /= currentItem.PackSize;
												if(currentItem.Weight == 0) currentItem.Weight = 1;

												GameServer.Database.AddObject(currentItem);

												createdItemsCount++;
											}

											currentMerchantItems.AddItem(currentItem ,pageNumber ,indexOnPage);
											#endregion
										
										}

										currentPacket = parser.GetNextPacket();
										currentPacketType = (byte)currentPacket.ReadByte();
										currentPacketId = (byte)currentPacket.ReadByte();
										currentPacket.Skip(2);
									}
									#endregion
	
									#region Merchant and ItemsList save

									string currentItemsListUniqueId = currentMerchantItems.GenerateUniqueId();
									DBMerchantTradeItems itemsList = (DBMerchantTradeItems) newMerchantItemsList[currentItemsListUniqueId];
									if(itemsList == null)
									{
										string newItemsListDbId = System.Guid.NewGuid().ToString();
										merchant.ItemsListTemplateID = newItemsListDbId;
										currentMerchantItems.ItemsListDBId = newItemsListDbId;

										foreach (DictionaryEntry item in currentMerchantItems.GetAllItems())
										{
											ItemTemplate currentitem = (ItemTemplate)item.Value;
											int itemPosition = (int)item.Key;

											MerchantItem newMerchantItem = new MerchantItem();
											newMerchantItem.ItemTemplateID = currentitem.Id_nb;
											newMerchantItem.PageNumber = itemPosition / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;
											newMerchantItem.SlotPosition = itemPosition % MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;
											newMerchantItem.ItemListID = newItemsListDbId;

											GameServer.Database.AddObject(newMerchantItem);
										}

										if(!newMerchantItemsList.Contains(currentItemsListUniqueId))
										{
											newMerchantItemsList.Add(currentItemsListUniqueId, currentMerchantItems);
										}
									}
									else
									{
										merchant.ItemsListTemplateID = itemsList.ItemsListDBId;
									}

									if(merchant.Guild.IndexOf(language.GetString("stable_master_string")) >= 0)
									{
										merchant.ClassType = "DOL.GS.GameStableMaster";
									}
									else
									{
										merchant.ClassType = "DOL.GS.GameMerchant";
									}

									GameServer.Database.SaveObject(merchant);

									tradeItemsListCount++;

									#endregion
								}
							}
								break;
						}

					}
						break;

					case (byte)ePacketType.Outcoming :  // traitement de tous les paquets sortants
					{
						switch(currentPacketId)
						{
							case 0x7A : // interact with mob
							{
								#region Find current merchant interact id

								Hashtable allFoundMerchants = (Hashtable)allFoundNpcIdByRegion[currentParsedRegionId];

								currentPacket.Skip(10);
								ushort npcInteractId = currentPacket.ReadShort();

								Mob merchant = (Mob) allFoundMerchants[npcInteractId];
								if(merchant != null)
								{
									currentMerchantId = npcInteractId;
								}

								#endregion
							}
								break;

							case 0xFC : // realm selection
							{
								#region Find Current Realm

								string accountName = currentPacket.ReadString(24);
								if(accountName.EndsWith("-S"))      currentRealm = eRealm.Albion;
								else if(accountName.EndsWith("-N")) currentRealm = eRealm.Midgard;
								else if(accountName.EndsWith("-H")) currentRealm = eRealm.Hibernia;

								#endregion
							}
								break;
						}
					}
						break;
				}
				currentPacket = parser.GetNextPacket();
			}

			sw.Close();

			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
 
		#endregion

		#region Npc and Equipment parse function
		/// <summary>
		/// Parse the .log file, extract all npc and equipemnt and add it to bd if necessary
		/// </summary>
		private void parseLogFileForMobAndEquipment(int version, ref  int mobCount,ref int inventoryCount,ref int itemsCount)
		{
			Hashtable newEquipmentsTemplates = precachingActualNpcEquipmentDb(); //(Generated id => CompleteEquipment)
			Hashtable allFoundNpcIdByRegion = new Hashtable(); //(regionid => array of mob id already found)

			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for npc and equipment...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			ushort currentParsedRegionId = 0;

			GSPacketIn currentPacket = parser.GetNextIncomingPacketWithId(0x20);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // don't use the packet lenght

				switch(currentPacketId)
				{
					case 0xDA : //npc create
					{
						#region Decode npc create and Inventory

						ushort currentMobId = currentPacket.ReadShort();

						ArrayList currentMobIdArray = (ArrayList)allFoundNpcIdByRegion[currentParsedRegionId];
						if(! currentMobIdArray.Contains(currentMobId))
						{
							ushort speed = currentPacket.ReadShort();
							ushort heading = currentPacket.ReadShort();
							int z = (int)currentPacket.ReadShort();

							int x = (int)currentPacket.ReadInt();
							int y = (int)currentPacket.ReadInt();

							/*if(IsDungeon(currentParsedRegionId))
							{
								currentPacket.Skip(2); //x and y on 2 byte for dungeon
								x = (int)currentPacket.ReadShort();
								currentPacket.Skip(2);
								y = (int)currentPacket.ReadShort();
							}
							else
							{
								x = (int)currentPacket.ReadInt();
								y = (int)currentPacket.ReadInt();
							}*/
						
							currentPacket.Skip(2);
							ushort model = currentPacket.ReadShort();
							byte size = (byte)currentPacket.ReadByte();
							byte level = (byte)currentPacket.ReadByte();	

							byte flags = 0;
							byte value1 = (byte)currentPacket.ReadByte(); // start flags decoding
							if((value1 & 0x01) != 0) flags |= (byte)GameNPC.eFlags.STEALTH;
							if((value1 & 0x20) != 0) flags |= (byte)GameNPC.eFlags.FLYING;
							byte realm = (byte) ((value1 & 0xC0) >>6);
							bool haveEquipment = (value1 & 0x02) != 0 ? true : false;

							byte maxStick = (byte)currentPacket.ReadByte();

							value1 = (byte)currentPacket.ReadByte(); // continue flags decoding
							if((value1 & 0x01) != 0) flags |= (byte)GameNPC.eFlags.CANTTARGET;
							if((value1 & 0x02) != 0) flags |= (byte)GameNPC.eFlags.DONTSHOWNAME;
							if((value1 & 0x04) != 0) flags |= (byte)GameNPC.eFlags.STEALTH;

							currentPacket.Skip(3); //unknown new in 1.71

							string name = currentPacket.ReadPascalString();
							string guildName = currentPacket.ReadPascalString();
							currentPacket.Skip(1);

							currentMobIdArray.Add(currentMobId);

							mobCount++;

							#region Decode npc equipement

							DBNpcInventory m_npcEquipment = null;

							if(haveEquipment) // start decode current npc equipment
							{
								currentPacket = parser.GetNextPacket();

								currentPacketType = (byte)currentPacket.ReadByte();
								currentPacketId = (byte)currentPacket.ReadByte();
								currentPacket.Skip(2);

								if(currentPacketType == (byte) ePacketType.Incoming && currentPacketId == 0x15) // to be sure it's a npc equipement packet
								{
									if(currentMobId == currentPacket.ReadShort()) // to be sure it's the good npc equipement
									{
										inventoryCount++;

										byte temp = (byte)currentPacket.ReadByte();
										currentPacket.Skip(2);
										bool cloakState = (temp & 0x01) == 1 ? true : false;
										byte activeQuiver = (byte) ((temp >> 4) & 0xF);
										byte activeWeapon = (byte)currentPacket.ReadByte();

										m_npcEquipment = new DBNpcInventory(name);
										temp = (byte)currentPacket.ReadByte(); // item count
										for(int i = 0 ; i < temp ; i++)
										{
											itemsCount++;

											NPCEquipment item = new NPCEquipment();
											item.Slot = (int)currentPacket.ReadByte();
											ushort baseModel = currentPacket.ReadShort();
											
                                            //if(item.Slot < (int)eInventorySlot.RightHandWeapon || item.Slot > (int)eInventorySlot.DistanceWeapon)
											//	currentPacket.Skip(1); // new in 172

                                            if (item.Slot > 13 || item.Slot < 10)
                                                item.Extension = currentPacket.ReadByte();


											item.Model =(int)(baseModel & 0x1FFF);

											item.Color = 0;
												
											if((baseModel & 0x8000) == 0x8000)
											{
												item.Color = (int)currentPacket.ReadShort();
											}
											else if((baseModel & 0x4000) == 0x4000)
											{
												item.Color = (int)currentPacket.ReadByte();
											}
												

											item.Effect = 0;
											if((baseModel & 0x2000) == 0x2000)
											{
												if(version < 176)
												{
													item.Effect = (int)currentPacket.ReadShort();
												}
												else
												{
													item.Effect = (int)currentPacket.ReadByte();
												}
											}

											m_npcEquipment.AddItem(item);

										}
									}
								}
							} // end decode npc equipement
							#endregion

							#region Save npc and inventory

							bool nouveau = false;	
							Mob currentNpc = GameServer.Database.SelectObject<Mob>("Region = '"+currentParsedRegionId+"' AND X = '"+x+"' AND Y = '"+y+"' AND Z = '"+z+"' AND Model = '"+model+"'");
							if(currentNpc == null)
							{
								currentNpc = new Mob();
								nouveau = true;
							}
									
							if(guildName.Equals(language.GetString("vault_keeper_string")))
							{
								currentNpc.ClassType = "DOL.GS.GameVaultKeeper";
							}
							else if(guildName.Equals(language.GetString("healer_string")))
							{
								currentNpc.ClassType = "DOL.GS.GameHealer";
							}
							else if(guildName.Equals(language.GetString("smith_string")))
							{
								currentNpc.ClassType = "DOL.GS.Blacksmith";
							}
							else if(guildName.Equals(language.GetString("enchanter_string")))
							{
								currentNpc.ClassType = "DOL.GS.Enchanter";
							}
									
									
							currentNpc.Speed = speed;
							currentNpc.Heading = heading;
							currentNpc.Z = z;
							currentNpc.X = x;
							currentNpc.Y = y;
							currentNpc.Model = model;
							currentNpc.Size = size;
							currentNpc.Level = level;
							currentNpc.Realm = realm;
							currentNpc.Flags = flags;
							currentNpc.Name = name;
							currentNpc.Guild = guildName;
							currentNpc.Region = currentParsedRegionId;
							currentNpc.AggroLevel = 0;
							currentNpc.AggroRange = 500;

							currentNpc.EquipmentTemplateID = ""; 
									
							if(m_npcEquipment != null)
							{
								string currentEquipmentUniqueId = m_npcEquipment.GenerateUniqueId();
								DBNpcInventory inventory = (DBNpcInventory) newEquipmentsTemplates[currentEquipmentUniqueId];
								if(inventory == null)
								{
									string newInventoryDbId = System.Guid.NewGuid().ToString();
									currentNpc.EquipmentTemplateID = newInventoryDbId;
									m_npcEquipment.EquipmentDBId = newInventoryDbId;
									foreach(NPCEquipment equipment in m_npcEquipment)
									{
										equipment.TemplateID = newInventoryDbId;
										GameServer.Database.AddObject(equipment);
									}
									newEquipmentsTemplates.Add(currentEquipmentUniqueId, m_npcEquipment);
								}
								else
								{
									currentNpc.EquipmentTemplateID = inventory.EquipmentDBId;
								}
							}

							if(nouveau)
							{
								GameServer.Database.AddObject(currentNpc);
							}
							else
							{
								GameServer.Database.SaveObject(currentNpc);
							}

							#endregion

						}

						#endregion
					}
						break;

					case 0x20 : // Set Player position and object id
					{
						#region Find current Region Id

						currentPacket.Skip(20); // This packet is used to find what region is in log
						currentParsedRegionId = currentPacket.ReadShort();

						if(! allFoundNpcIdByRegion.Contains(currentParsedRegionId))
						{
							allFoundNpcIdByRegion.Add(currentParsedRegionId, new ArrayList());
						}

						#endregion
					}
						break;
				}
		
				currentPacket = parser.GetNextIncomingPacket();
			}
			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
		#endregion

		#region Db cleaning function
		/// <summary>
		/// Parse the .log file, extract all npc and equipemnt and add it to bd if necessary
		/// </summary>
		private void parseLogFileAndCleanDb(int version, ref  int deletedMobCount, ref int deletedItemsCount)
		{
			if (log.IsInfoEnabled)
				log.Info("Begin parse .log file for cleaning db ...");

			// general current packet informations
			byte currentPacketType = 0;
			byte currentPacketId = 0;

			// general positional informations
			ushort currentParsedRegionId = 0;
			Mob lastFoundSteed = null; // used to store the last horse used to ride in order to delete it at the end


			GSPacketIn currentPacket = parser.GetNextIncomingPacketWithId(0x20);	
			while(currentPacket != null)
			{
				currentPacketType = (byte)currentPacket.ReadByte();
				currentPacketId = (byte)currentPacket.ReadByte();
				currentPacket.Skip(2); // i don't use the packet lenght

				switch(currentPacketType)
				{
					case (byte)ePacketType.Incoming :  // traitement de tous les paquets entrants
					{
						switch(currentPacketId)
						{
							case 0x20 : // Set Player position and object id
							{
								#region Find current Region Id

								currentPacket.Skip(20); // This packet is used to find what region is in log
								currentParsedRegionId = currentPacket.ReadShort();

								#endregion
							}
								break;

							case 0xDA:
							{
								#region Decode npc create

								ushort mobGameId = currentPacket.ReadShort();
								ushort speed = currentPacket.ReadShort();
								ushort heading = currentPacket.ReadShort();
								int z = (int)currentPacket.ReadShort();
								int x = (int)currentPacket.ReadInt();
								int y = (int)currentPacket.ReadInt();

								/*if(IsDungeon(currentParsedRegionId))
								{
									currentPacket.Skip(2); //x and y on 2 byte for dungeon
									x = (int)currentPacket.ReadShort();
									currentPacket.Skip(2);
									y = (int)currentPacket.ReadShort();
								}
								else
								{
									x = (int)currentPacket.ReadInt();
									y = (int)currentPacket.ReadInt();
								}*/
									
								currentPacket.Skip(2); // speed z
								ushort model = currentPacket.ReadShort();
								byte size = (byte)currentPacket.ReadByte();
								byte level = (byte)currentPacket.ReadByte();	

								byte flags = (byte)currentPacket.ReadByte(); // start flag decoding
								byte realm = (byte) ((flags & 0xC0) >>6);
								bool haveEquipment = (flags & 0x02) != 0 ? true : false;
								flags = (byte)(flags & 0x21);	

								byte maxStick = (byte)currentPacket.ReadByte();

								currentPacket.Skip(4); //unknown new in 1.71
								string name = currentPacket.ReadPascalString();
								string guildName = currentPacket.ReadPascalString();
								currentPacket.Skip(1);	

								if(IsSteed(name, model)) // delete all not useful steed
								{
									if(speed == 0) // horse used by player who made the log and static stable master horse
									{
										Mob newSteed = new Mob();
										newSteed.AggroLevel = mobGameId;
										newSteed.Speed = speed;
										newSteed.Heading = heading; 
										newSteed.Z = z; 
										newSteed.X = x; 
										newSteed.Y = y; 
										newSteed.Model = model; 
										newSteed.Size = size; 
										newSteed.Level = level; 
										newSteed.Realm = realm; 
										newSteed.Flags = flags;
										newSteed.Name = name; 
										newSteed.Guild = guildName; 
										newSteed.Region = currentParsedRegionId;

										lastFoundSteed = newSteed;
									}
									else // other running horse
									{
										Mob currentSteed = GameServer.Database.SelectObject<Mob>("Name = '"+ GameServer.Database.Escape(name) +"'AND Guild = '"+GameServer.Database.Escape(guildName)+"' AND Region = '"+currentParsedRegionId+"' AND Realm = '"+realm+"' AND X = '"+x+"' AND Y = '"+y+"' AND Z = '"+z+"' AND Heading ='"+heading+"' AND Model = '"+model+"' AND Level = '"+level+"' AND Size = '"+size+"' AND Speed = '"+speed+"'");
										if(currentSteed != null)
										{
											GameServer.Database.DeleteObject(currentSteed);
											deletedMobCount++;
										}
									}
								}
								else if(guildName.EndsWith(language.GetString("pet_string")))  // delete all pets	
								{
									Mob currentPet = GameServer.Database.SelectObject<Mob>( "Guild = '" +GameServer.Database.Escape(guildName)+ "'");
									if(currentPet != null)
									{
										GameServer.Database.DeleteObject(currentPet);
										deletedMobCount++;
									}
								}
								else if(model == 666) // invisible mob
								{
									Mob currentInvi = GameServer.Database.SelectObject<Mob>( "Model = '" +model+ "'");
									if(currentInvi != null)
									{
										GameServer.Database.DeleteObject(currentInvi);
										deletedMobCount++;
									}
								}

								#endregion
							}
								break;

							case 0xC8 : // Player ride
							{
								#region Find current horse
								
								currentPacket.Skip(2);
								ushort horseId = currentPacket.ReadShort();
								bool riding = currentPacket.ReadByte() == 0 ? false : true;

								if(lastFoundSteed != null && lastFoundSteed.AggroLevel == horseId && riding)
								{
									Mob currentSteed = GameServer.Database.SelectObject<Mob>("Name = '"+ GameServer.Database.Escape(lastFoundSteed.Name) +"'AND Guild = '"+GameServer.Database.Escape(lastFoundSteed.Guild)+"' AND Region = '"+lastFoundSteed.Region+"' AND Realm = '"+lastFoundSteed.Realm+"' AND X = '"+lastFoundSteed.X+"' AND Y = '"+lastFoundSteed.Y+"' AND Z = '"+lastFoundSteed.Z+"' AND Heading ='"+lastFoundSteed.Heading+"' AND Model = '"+lastFoundSteed.Model+"' AND Level = '"+lastFoundSteed.Level+"' AND Size = '"+lastFoundSteed.Size+"' AND Speed = '"+lastFoundSteed.Speed+"'");
									if(currentSteed != null)
									{
										GameServer.Database.DeleteObject(currentSteed);
										deletedMobCount++;
									}
									lastFoundSteed = null;
								}
								#endregion
							}
								break;

							case 0xD9 : // Object Creation
							{
								#region Decode world object create

								currentPacket.Skip(2); // internal object id
								ushort emblem = currentPacket.ReadShort();
								ushort heading = currentPacket.ReadShort();
								int z = (int)currentPacket.ReadShort();
								int x = (int)currentPacket.ReadInt();
								int y = (int)currentPacket.ReadInt();

								/*if(IsDungeon(currentParsedRegionId))
								{
									currentPacket.Skip(2); //x and y on 2 byte for dungeon
									x = (int)currentPacket.ReadShort();
									currentPacket.Skip(2);
									y = (int)currentPacket.ReadShort();
								}
								else
								{
									x = (int)currentPacket.ReadInt();
									y = (int)currentPacket.ReadInt();
								}*/

								ushort model = currentPacket.ReadShort();
								ushort objectType = currentPacket.ReadShort();  //(4 high bits : object realm) + (4 low bits : 8 static object (Banner, campfire), 4 player loot, 0 Lathe, Alchemy Table, Forge, Battle Grounds Portal Stone etc and loot drops for other players)
								currentPacket.Skip(4);
								string name = currentPacket.ReadPascalString();

								if(name.IndexOf(language.GetString("grave_string")) > 0 || name.IndexOf(language.GetString("bag_of_coins_string")) >= 0 || (objectType & 0x04) != 0 ) 	
								{ 
									DataObject[] allBadObjects = (DataObject[])GameServer.Database.SelectObjects<WorldObject>( "Name = '" +GameServer.Database.Escape(name)+ "' AND X = '"+x+"' AND Y = '"+y+"' AND Z = '"+z+"' AND Heading = '"+heading+"' AND Region = '"+currentParsedRegionId+"'");
									foreach(WorldObject currentWorldObject in allBadObjects)
									{
										GameServer.Database.DeleteObject(currentWorldObject);
										deletedItemsCount++;
									}
								}
								#endregion
							}
								break;
						}
					}
						break;
				}
				
				currentPacket = parser.GetNextIncomingPacket();
			}
			if (log.IsInfoEnabled)
				log.Info("File parsing ended.");
		}
		#endregion

		#region Prechaching actual db
		/// <summary>
		/// Return the npc equipment db in hashtable : (Generated id => CompleteEquipment)
		/// </summary>
		private Hashtable precachingActualNpcEquipmentDb()
		{
			if (log.IsInfoEnabled)
				log.Info("Precaching old npc equipment database starting ...");

			// load the old ncp equipment db
			Hashtable oldEquipmentsTemplates = new Hashtable();
			NPCEquipment[] oldEquipments = (NPCEquipment[])GameServer.Database.SelectAllObjects<NPCEquipment>();
			foreach(NPCEquipment equipment in oldEquipments)
			{
				DBNpcInventory inventory = (DBNpcInventory) oldEquipmentsTemplates[equipment.TemplateID];
				if(inventory == null)
				{
					inventory = new DBNpcInventory(equipment.TemplateID);
					oldEquipmentsTemplates.Add(equipment.TemplateID, inventory);
				}
				inventory.AddItem(equipment);
			}

			Hashtable newEquipmentsTemplates = new Hashtable();
			foreach (DBNpcInventory oldEquipment in oldEquipmentsTemplates.Values)
			{
				string uniqueId = oldEquipment.GenerateUniqueId();
				DBNpcInventory inventory = (DBNpcInventory) newEquipmentsTemplates[uniqueId];
				if(inventory == null)
				{
					newEquipmentsTemplates.Add(uniqueId ,oldEquipment);
				}
				else
				{
					//if (log.IsInfoEnabled)
					//	log.Info("Npc inventory template with id ("+inventory.EquipmentDBId+") defined twice in db.");
					//maybe remplace here the old one
				}
			}
			if (log.IsInfoEnabled)
				log.Info("Old npc equipment database precaching ended.");

			return newEquipmentsTemplates; 
		}


		/// <summary>
		/// Return the merchant items list db in hashtable : (Generated id => ItemsList)
		/// </summary>
		private Hashtable precachingActualMerchantItemsDb()
		{
			if (log.IsInfoEnabled)
				log.Info("Precaching old merchant items database starting ...");

			// load the old ncp inventory db
			Hashtable oldItemsList = new Hashtable();
			MerchantItem[] oldItems = (MerchantItem[])GameServer.Database.SelectAllObjects<MerchantItem>();
			foreach(MerchantItem item in oldItems)
			{
				DBMerchantTradeItems itemsList = (DBMerchantTradeItems) oldItemsList[item.ItemListID];
				if(itemsList == null)
				{
					itemsList = new DBMerchantTradeItems(item.ItemListID);
					oldItemsList.Add(item.ItemListID, itemsList);
				}
				ItemTemplate myItem = GameServer.Database.FindObjectByKey<ItemTemplate>(item.ItemTemplateID);
				if(item != null)
				{
					itemsList.AddItem(myItem, item.PageNumber , item.SlotPosition);
				}
				else
				{
					if (log.IsErrorEnabled)
						log.Error("Merchant item db : item with id ("+item.ItemTemplateID+") not found in ItemTemplate db");
				}
			}
					
			Hashtable newItemsList = new Hashtable();
			foreach (DBMerchantTradeItems oldMerchantItemsList in oldItemsList.Values)
			{
				string uniqueId = oldMerchantItemsList.GenerateUniqueId();
				DBMerchantTradeItems itemsList = (DBMerchantTradeItems) newItemsList[uniqueId];
				if(itemsList == null)
				{
					newItemsList.Add(uniqueId ,oldMerchantItemsList);
				}
			}
			if (log.IsInfoEnabled)
				log.Info("Old merchant items database precaching ended.");

			return newItemsList; 
		}

		#endregion

		#region Healper class

		/// <summary>
		/// A complete npc equipment
		/// </summary>
		private sealed class DBNpcInventory
		{		
			private HybridDictionary  m_items;

			private string equipementDBIdentifier;

			/// <summary>
			/// Constructs new PagedItemsCollection
			/// </summary>
			/// <param name="templateId"></param>
			public DBNpcInventory(string templateId)
			{
				m_items = new HybridDictionary();
				equipementDBIdentifier = templateId;
			}

			/// <summary>
			/// Gets the db ID of the equipment
			/// </summary>
			public string EquipmentDBId
			{
				get { return equipementDBIdentifier; }
				set { equipementDBIdentifier = value; }
			}

			/// <summary>
			/// Adds item to the equipment
			/// </summary>
			/// <param name="id"></param>
			public void AddItem(NPCEquipment item)
			{
				if (m_items.Contains(item.Slot))
				{
					if (log.IsErrorEnabled)
						log.Error("Item with model ("+item.Model+") already in slot ("+item.Slot+") in npc inventory template ("+equipementDBIdentifier+")");
					return;
				}
				m_items.Add(item.Slot, item);
			}

			/// <summary>
			/// Made the unique inventotyid identifier
			/// </summary>
			public string GenerateUniqueId()
			{
				string uniqueID="";
				foreach (NPCEquipment item in m_items.Values)
				{
					uniqueID +="["+item.Slot+":"+item.Model+":"+item.Color+":"+item.Effect+"]";
				}
				return uniqueID;
			}

			public IEnumerator GetEnumerator()
			{
				return m_items.Values.GetEnumerator();
			}
		}

		/// <summary>
		/// A complete merchant trade items list
		/// </summary>
		private sealed class DBMerchantTradeItems
		{		
			private HybridDictionary  m_items;

			private string itemsListDBIdentifier;

			/// <summary>
			/// Constructs new PagedItemsCollection
			/// </summary>
			/// <param name="templateId"></param>
			public DBMerchantTradeItems(string templateId)
			{
				m_items = new HybridDictionary();
				itemsListDBIdentifier = templateId;
			}

			/// <summary>
			/// Gets the db ID of the equipment
			/// </summary>
			public string ItemsListDBId
			{
				get { return itemsListDBIdentifier; }
				set { itemsListDBIdentifier = value; }
			}

			/// <summary>
			/// Adds item to the list
			/// </summary>
			/// <param name="id"></param>
			public void AddItem(ItemTemplate item, int page, int slot)
			{
				int finalPosition = page * MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS + slot;
				if (m_items.Contains(finalPosition))
				{
					if (log.IsErrorEnabled)
						log.Error("Item with templateid ("+item.Id_nb+") already in page/slot ("+page+"/"+slot+") in merchant items list ("+itemsListDBIdentifier+")");
					return;
				}
				m_items.Add(finalPosition, item);
			}

			/// <summary>
			/// Made the unique inventotyid identifier
			/// </summary>
			public string GenerateUniqueId()
			{
				string uniqueID="";
				foreach (DictionaryEntry item in m_items)
				{
					ItemTemplate currentitem = (ItemTemplate)item.Value;
					if(currentitem != null)
					{
						int itemPosition = (int)item.Key;
						uniqueID = uniqueID+"["+currentitem.Id_nb+":"+itemPosition / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS+":"+itemPosition % MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS+"]";
						//[itemtemplateid:page:slot]
					}
				}
				return uniqueID;
			}

			/// <summary>
			/// Return the first item found with the given name
			/// </summary>
			public ItemTemplate GetItemByName(string name)
			{
				foreach(ItemTemplate item in m_items.Values)
				{
					if(item != null && item.Name == name)
					{
						return item;
					}
				}
				return null;
			}

			/// <summary>
			/// Return the main HybridDictionary
			/// </summary>
			public HybridDictionary GetAllItems()
			{
				return m_items;
			}
		}

		#endregion

		#region Function divers

		#region Magical object and Armor type
		private static int GetItemTypeFromModel(string name, int model)
		{
			// fist time make a test with know item model and after with name
			switch(model)
			{
				// poison model
				case 2538: case 2539: case 2540: case 2541: 
				case 2542: case 2543: case 2544: case 2545: case 2546: case 2547:
				case 2548: case 2549: case 2550:
				// renewal model
				case 99:
				// siege ammunition
				case 514: case 2620: case 2621: case 2622:
				{
					//Generique non equipable magical items (poison , renewal ,siege armmunition if it's a unique model)
					return 40;
				}

				case 57: case 91: case 92: case 96: case 326: case 443:
				case 467: case 557: case 558: case 560: case 669: case 676: 
				case 677: case 678: case 1720: case 1721: case 1722: case 1723:
				case 1724: case 1725: case 1726: case 1727:
				{
					//Cloak
					return 26;
				} 
					
				case 101: case 623: case 624: case 1887:
				{
					//Necklace
					return 29;
				}
					
				case 597: case 1884:
				{
					//Waist == belt? => Yes
					return 32;
				}
					 
				case 598: case 619: case 622: case 1885:
				{
					//Bracer
					return 33;
				}
			
				case 103: case 109: case 483: case 484: case 485:
				case 486: case 487: case 1888: case 1891: case 2520: 
				case 2521: case 2522: case 2523: case 2524: case 2525:
				case 2526:
				{
					//Ring
					return 35;
				}
					
				case 104: case 106: case 110: case 111: case 112: case 113:
				case 114: case 115: case 116: case 117: case 118: case 119:
				case 496: case 497: case 498: case 500: case 503: case 509:
				case 523: case 524: case 525: case 526: case 531:
				case 540: case 549: case 592: case 593: case 618: case 620:
				case 621: case 630: case 1886:
				{
					// Jewellery
					return 24;
				}

				default :
					return 40;
			}

			/*if(name.IndexOf("Poison") >= 0 || name.IndexOf("poison") >= 0
			|| name.IndexOf("Venom") >= 0 || name.IndexOf("venom") >= 0
			|| name.IndexOf("Serum") >= 0 || name.IndexOf("serum") >= 0
			|| name.IndexOf("Lifebane") >= 0 || name.IndexOf("lifebane") >= 0
			|| name.IndexOf("Renewal") >= 0 || name.IndexOf("renewal") >= 0)
			{
				//poison
				return 40;
			}
			else if(name.IndexOf("Cloak") >= 0 || name.IndexOf("cloak") >= 0 
				|| name.IndexOf("Mantle") >= 0 || name.IndexOf("mantle") >= 0
				|| name.IndexOf("Shawl") >= 0 || name.IndexOf("shawl") >= 0
				|| name.IndexOf("Cape") >= 0 || name.IndexOf("cape") >= 0)
			{
				//Cloak
				return 26;
			}
			else if(name.IndexOf("Necklace") >= 0 || name.IndexOf("necklace") >= 0 
				|| name.IndexOf("Pendant") >= 0 || name.IndexOf("pendant") >= 0
				|| name.IndexOf("Medallion") >= 0 || name.IndexOf("medallion") >= 0
				|| name.IndexOf("Chain") >= 0 || name.IndexOf("chain") >= 0
				|| name.IndexOf("Amulet") >= 0 || name.IndexOf("amulet") >= 0
				|| name.IndexOf("Brooch") >= 0 || name.IndexOf("brooch") >= 0
				|| name.IndexOf("Collar") >= 0 || name.IndexOf("collar") >= 0)
			{
				//Necklace
				return 29;
			}
			else if(name.IndexOf("Belt") >= 0 || name.IndexOf("belt") >= 0 
				|| name.IndexOf("Girdle") >= 0 || name.IndexOf("girdle") >= 0
				|| name.IndexOf("Bann") >= 0 || name.IndexOf("bann") >= 0)
			{
				//Waist
				return 32;
			}
			else if(name.IndexOf("Bracer") >= 0 || name.IndexOf("bracer") >= 0 
				|| name.IndexOf("Bracelet") >= 0 || name.IndexOf("bracelet") >= 0
				|| ((name.IndexOf("Giant") >= 0 || name.IndexOf("giant") >= 0) && (name.IndexOf("Ring") >= 0 || name.IndexOf("ring") >= 0)))
			{
				//Bracer
				return 33;
			}
			else if(name.IndexOf("Ring") >= 0 || name.IndexOf("ring") >= 0)
			{
				//ring
				return 35;
			}
			else if(name.IndexOf("Jewel") >= 0 || name.IndexOf("jewel") >= 0 
				|| name.IndexOf("Scroll") >= 0 || name.IndexOf("scroll") >= 0
				|| name.IndexOf("Diamond") >= 0 || name.IndexOf("diamond") >= 0
				|| name.IndexOf("Talisman") >= 0 || name.IndexOf("talisman") >= 0
				|| name.IndexOf("Ruby") >= 0 || name.IndexOf("ruby") >= 0
				|| name.IndexOf("Crystal") >= 0 || name.IndexOf("crystal") >= 0
				|| name.IndexOf("Gem") >= 0 || name.IndexOf("gem") >= 0
				|| name.IndexOf("Coin") >= 0 || name.IndexOf("coin") >= 0
				|| name.IndexOf("Sapphire") >= 0 || name.IndexOf("sapphire") >= 0
				|| name.IndexOf("Stone") >= 0 || name.IndexOf("stone") >= 0
				|| name.IndexOf("Spellbook") >= 0 || name.IndexOf("spellbook") >= 0
				|| name.IndexOf("Eye") >= 0 || name.IndexOf("eye") >= 0)
			{
				//Jewellery
				return 24;
			}
			else
			{
				return 40;
			}*/
		}

		private static int GetArmorTypeFromModel(string name, int model)
		{
			// fist time make a test with know item model and after with name
			switch(model)
			{
				case 35: case 62: case 63: case 64: case 93: case 94: 
				case 95: case 335: case 336: case 337: case 438: case 439:
				case 440: case 491: case 492: case 493: case 511: case 596:
				case 627: case 822: case 823: case 824: case 825: case 826:
				case 827: case 829: case 830: case 831: case 832: case 833:
				case 834: case 835: case 836: case 837: case 838: case 839:
				case 840: case 1197: case 1198: case 1199: case 1200: case 1201:
				case 1202:case 1203: case 1204: case 1205: case 1206: case 1207:
				case 1208: case 1209: case 1210: case 1211: case 1212: case 1213:
				case 1214: case 1215: case 1216: case 1217: case 1218: case 1219:
				case 1220: case 1221: case 1222: case 1223: case 1224: case 1225:
				case 1226: case 1227: case 1228: case 1229: case 1230: case 1231:
				case 1232: case 1233: case 1234: case 1235: case 1236: case 1237:
				case 1238: case 1239: case 1240: case 1241: case 1242: case 1243:
				case 1244: case 1245: case 1277: case 1278: case 1279: case 1280:
				case 1281: case 1282: case 1283: case 1284: case 1285: case 1286:
				case 1287: case 1288: case 1289: case 1290: case 1291: case 1292:
				case 1294: case 1295: case 1296: case 1297: case 1298: case 1299:
				case 1839: case 1840: case 1841: case 1842: case 1843: case 1844:
				case 2223: case 2224: case 2225: case 2251: case 2252: case 2253:
				case 2254: case 2255: case 2256: case 2257: case 2258: case 2259:
				case 2260: case 2261: case 2262: case 2263: case 2264: case 2265:
				case 2266: case 2267: case 2268: case 2269: case 2270: case 2271:
				case 2272: case 2273: case 2274: case 2275: case 2276: case 2277:
				case 2278: case 2279: case 2280: case 2281: case 2282: case 2283:
				case 2284: case 2285: case 2286: case 2287: case 2288: case 2289:
				case 2290: case 2291: case 2292: case 2293: case 2294: case 2295:
				case 2296: case 2297: case 2298: case 2299: case 2300: case 2301:
				case 2302: case 2303: case 2304: case 2305: case 2306: case 2307:
				case 2308: case 2309: case 2310: case 2311: case 2312: case 2313:
				case 2314: case 2315: case 2316: case 2317: case 2318: case 2319:
				case 2320: case 2321: case 2322: case 2323: case 2324: case 2325:
				case 2326: case 2327: case 2328: case 2329: case 2330: case 2331:
				case 2332: case 2333: case 2334: case 2335: case 2336: case 2337:
				case 2338: case 2339: case 2340: case 2341: case 2342: case 2343:
				case 2344: case 2345: case 2346: case 2347: case 2348: case 2349:
				case 2350: case 2351: case 2352: case 2353: case 2354: case 2355:
				case 2356: case 2357: case 2358: case 2359: case 2360: case 2361:
				case 2362: case 2363: case 2364: case 2365: case 2366: case 2367:
				case 2368: case 2369: case 2370: case 2371: case 2372: case 2373:
				case 2374: case 2375: case 2376: case 2377: case 2378: case 2379:
				case 2380: case 2381: case 2382: case 2383: case 2384: case 2385:
				case 2386: case 2387: case 2388: case 2389: case 2390: case 2391:
				case 2392: case 2393: case 2394: case 2395: case 2396: case 2397:
				case 2398: case 2399: case 2400: case 2401: case 2402: case 2403:
				case 2404: case 2405: case 2406: case 2407: case 2408: case 2409:
				case 2410: case 2411: case 2412: case 2413: case 2414: case 2415:
				case 2416: case 2417: case 2418: case 2419: case 2420: case 2421:
				case 2422: case 2423: case 2424: case 2425: case 2426: case 2427:
				case 2428: case 2429: case 2430: case 2431: case 2432: case 2433:
				case 2434: case 2435: case 2436: case 2437: case 2438: case 2439:
				case 2440: case 2441: case 2442: case 2443: case 2444: case 2445:
				case 2446: case 2447: case 2448: case 2449: case 2450: case 2451:
				case 2452: case 2453: case 2454: case 2455: case 2456: case 2457:
				case 2458: case 2459: case 2460: case 2461: case 2462: case 2463:
				case 2464: case 2465: case 2466: case 2698: case 2704: case 2710:
				case 2716: case 2722: case 2732: case 2738: case 2744: case 2750:
				case 2756: case 2766: case 2772: case 2778: case 2784: case 2790:
				case 2800: case 2806: case 2812: case 2818: case 2824: case 2834:
				case 2840: case 2846: case 2852: case 2858: case 2868: case 2874:
				case 2880: case 2886: case 2892: case 2951: 
				{ // HeadArmor
					return 21;
				}
					
				case 34: case 39: case 44: case 49: case 77: case 80:
				case 85: case 89: case 137: case 142: case 149: case 154:
				case 159: case 164: case 169: case 174: case 179: case 184:
				case 189: case 194: case 199: case 204: case 209: case 214:
				case 219: case 224: case 233: case 238: case 243: case 248:
				case 253: case 258: case 263: case 268: case 273: case 278:
				case 283: case 288: case 293: case 298: case 303: case 308:
				case 341: case 346: case 351: case 356: case 361: case 366:
				case 371: case 376: case 381: case 386: case 391: case 396:
				case 401: case 406: case 411: case 416: case 421: case 426:
				case 431: case 436: case 665: case 691: case 696: case 701:
				case 706: case 711: case 716: case 721: case 726: case 732:
				case 737: case 742: case 749: case 754: case 759: case 764:
				case 769: case 774: case 779: case 785: case 790: case 795:
				case 802: case 808: case 813: case 818: case 986: case 991:
				case 996: case 1000: case 1191: case 1195: case 1249: case 1254:
				case 1259: case 1263: case 1271: case 1275: case 1302: case 1311:
				case 1334: case 1558: case 1620: case 1622: case 1624: case 1645:
				case 1690: case 1699: case 1708: case 1717: case 1741: case 1753: 
				case 1762: case 1776: case 1785: case 1794: case 1803: case 1814:
				case 2097: case 2106: case 2129: case 2140: case 2149: case 2181:
				case 2235: case 2248: case 2249: case 2250: case 2633: case 2643: 
				case 2700: case 2706: case 2712: case 2718: case 2724: case 2734:
				case 2740: case 2746: case 2752: case 2758: case 2768: case 2774:
				case 2780: case 2786: case 2792: case 2802: case 2808: case 2814:
				case 2820: case 2826: case 2836: case 2842: case 2848: case 2854:
				case 2860: case 2870: case 2876: case 2882: case 2888: case 2894:
				case 2926: case 2931: case 2936: case 2941: case 2945: case 2950: 
				{  //HandsArmor
					return 22;
				}
					
				case 40: case 45: case 50: case 54: case 78: case 84: case 90:
				case 133: case 138: case 143: case 150: case 155: case 160:
				case 165: case 170: case 175: case 180: case 185: case 190:
				case 195: case 200: case 205: case 210: case 215: case 220:
				case 225: case 234: case 239: case 244: case 249: case 254:
				case 259: case 264: case 269: case 274: case 279: case 284:
				case 289: case 294: case 299: case 304: case 309: case 342:
				case 347: case 352: case 357: case 362: case 367: case 372:
				case 377: case 382: case 387: case 392: case 397: case 402:
				case 407: case 412: case 417: case 422: case 427: case 432:
				case 437: case 666: case 692: case 697: case 702: case 707: 
				case 712: case 717: case 722: case 727: case 731: case 738:
				case 743: case 750: case 755: case 760: case 765: case 770:
				case 775: case 780: case 786: case 791: case 796: case 803:
				case 809: case 814: case 819: case 987: case 992: case 997:
				case 1001: case 1190: case 1196: case 1250: case 1255: case 1260: 
				case 1264: case 1270: case 1276: case 1301: case 1310: case 1333:
				case 1557: case 1629: case 1630: case 1643: case 1644: case 1688:
				case 1689: case 1697: case 1698: case 1706: case 1707: case 1715:
				case 1716: case 1739: case 1740: case 1751: case 1752: case 1760:
				case 1761: case 1774: case 1775: case 1783: case 1784: case 1792:
				case 1793: case 1801: case 1802: case 1812: case 1813: case 1851:
				case 1852: case 2095: case 2096: case 2104: case 2105: case 2127:
				case 2128: case 2138: case 2139: case 2147: case 2156: case 2157:
				case 2165: case 2166: case 2179: case 2180: case 2236: case 2241:
				case 2242: case 2634: case 2639: case 2644: case 2699: case 2705:
				case 2711: case 2717: case 2723: case 2733: case 2739: case 2745:
				case 2751: case 2757: case 2767: case 2773: case 2779: case 2785:
				case 2791: case 2801: case 2807: case 2813: case 2819: case 2825:
				case 2835: case 2841: case 2847: case 2853: case 2859: case 2869:
				case 2875: case 2881: case 2887: case 2893: case 2927: case 2932:
				case 2937: case 2942: case 2946: case 2952:
				{ //FeetArmor
					return 23;
				}
					
				case 31: case 36: case 41: case 46: case 51: case 58:
				case 74: case 81: case 86: case 97: case 98: case 134: 
				case 139: case 146: case 151: case 156: case 161: case 166: 
				case 171: case 176: case 181: case 186: case 191: case 196: 
				case 201: case 206: case 211: case 216: case 221: case 230:
				case 235: case 240: case 245: case 250: case 255: case 260: 
				case 265: case 270: case 275: case 280: case 285: case 290: 
				case 295: case 300: case 305: case 338: case 343: case 348:
				case 353: case 358: case 363: case 368: case 373: case 378:
				case 383: case 388: case 393: case 398: case 403: case 408:
				case 413: case 418: case 423: case 428: case 433: case 441: 
				case 662: case 667: case 668: case 682: case 683: case 684:
				case 685: case 686: case 687: case 688: case 693: case 698:
				case 703: case 708: case 713: case 718: case 723: case 728:
				case 733: case 734: case 739: case 744: case 745: case 746:
				case 751: case 756: case 761: case 766: case 771: case 776:
				case 781: case 782: case 787: case 792: case 797: case 798:
				case 799: case 804: case 805: case 810: case 815: case 983:
				case 988: case 993: case 999: case 1003: case 1005: case 1006:
				case 1007: case 1008: case 1186: case 1187: case 1192: case 1246:
				case 1251: case 1256: case 1262: case 1266: case 1267: case 1272:
				case 1300: case 1304: case 1305: case 1309: case 1313: case 1314: 
				case 1332: case 1336: case 1337: case 1554: case 1619: case 1621: 
				case 1623: case 1626: case 1627: case 1628: case 1640: case 1641: 
				case 1642: case 1683: case 1685: case 1686: case 1687: case 1694:
				case 1695: case 1696: case 1703: case 1704: case 1705: case 1712:
				case 1713: case 1714: case 1736: case 1737: case 1738: case 1748:
				case 1749: case 1750: case 1757: case 1758: case 1759: case 1771: 
				case 1772: case 1773: case 1780: case 1781: case 1782: case 1789:
				case 1790: case 1791: case 1798: case 1799: case 1800: case 1809:
				case 1810: case 1811: case 1848: case 1849: case 1850: case 2092: 
				case 2093: case 2094: case 2101: case 2102: case 2103: case 2120:
				case 2121: case 2122: case 2124: case 2125: case 2126: case 2135:
				case 2136: case 2137: case 2144: case 2145: case 2146: case 2153: 
				case 2154: case 2155: case 2160: case 2162: case 2163: case 2164:
				case 2169: case 2170: case 2171: case 2172: case 2173: case 2174: 
				case 2176: case 2177: case 2178: case 2221: case 2222: case 2226: 
				case 2227: case 2228: case 2230: case 2231: case 2232: case 2238:
				case 2239: case 2240: case 2245: case 2246: case 2247: case 2630:
				case 2635: case 2640: case 2694: case 2695: case 2701: case 2707:
				case 2713: case 2719: case 2728: case 2729: case 2735: case 2741:
				case 2747: case 2753: case 2762: case 2763: case 2769: case 2775:
				case 2781: case 2787: case 2796: case 2797: case 2803: case 2809:
				case 2815: case 2821: case 2830: case 2831: case 2837: case 2843:
				case 2849: case 2855: case 2864: case 2865: case 2871: case 2877:
				case 2883: case 2889: case 2921: case 2922: case 2923: case 2928:
				case 2933: case 2938:
				{ //TorsoArmor
					return 25;
				}
																			
				case 32: case 37: case 42: case 47: case 52: case 57: case 75:
				case 82: case 87: case 135: case 140: case 147: case 152:
				case 157: case 162: case 167: case 172: case 177: case 182:
				case 187: case 192: case 197: case 202: case 207: case 212:
				case 217: case 222: case 231: case 236: case 241: case 246:
				case 251: case 256: case 261: case 266: case 271: case 276:
				case 281: case 286: case 291: case 296: case 301: case 306:
				case 339: case 344: case 349: case 354: case 359: case 364:
				case 369: case 374: case 379: case 384: case 389: case 394:
				case 399: case 404: case 409: case 414: case 419: case 424:
				case 429: case 434: case 663: case 689: case 694: case 699:
				case 704: case 709: case 714: case 719: case 724: case 729: 
				case 735: case 740: case 747: case 752: case 757: case 762: 
				case 767: case 772: case 777: case 783: case 788: case 793: 
				case 800: case 806: case 811: case 816: case 984: case 989: 
				case 994: case 998: case 1188: case 1193: case 1247:  case 1252: 
				case 1257: case 1261: case 1268: case 1273: case 1303: case 1312:
				case 1335: case 1555: case 1631: case 1632: case 1646: case 1647:
				case 1691: case 1692: case 1700: case 1701: case 1709: case 1710:
				case 1718: case 1719: case 1742: case 1743: case 1744: case 1745:
				case 1754: case 1755: case 1763: case 1777: case 1778: case 1786: 
				case 1787: case 1795: case 1796: case 1804: case 1805: case 1815:
				case 1816: case 1854: case 1855: case 2098: case 2099: case 2107:
				case 2108: case 2130: case 2131: case 2141: case 2142: case 2150:
				case 2151: case 2158: case 2159: case 2167: case 2168: case 2182:
				case 2183: case 2234: case 2243: case 2244: case 2631: case 2636:
				case 2641: case 2696: case 2702: case 2708: case 2714: case 2720:
				case 2730: case 2736: case 2742: case 2748: case 2754: case 2764:
				case 2770: case 2776: case 2782: case 2788: case 2798: case 2804:
				case 2810: case 2816: case 2822: case 2832: case 2838: case 2844:
				case 2850: case 2856: case 2866: case 2872: case 2878: case 2884:
				case 2890: case 2924: case 2929: case 2934: case 2939: case 2943: 
				case 2949:
				{  //LegsArmor
					return 27;
				}
					
				case 28: case 33: case 38: case 43: case 48: case 53: case 76:
				case 83: case 88: case 120: case 121: case 122: case 123: case 124:
				case 125: case 126: case 127: case 128: case 129: case 130:
				case 131: case 136: case 141: case 148: case 153: case 158:
				case 163: case 168: case 173: case 178: case 183: case 188:
				case 193: case 198: case 203: case 208: case 213: case 218:
				case 223: case 232: case 237: case 242: case 247: case 252:
				case 257: case 262: case 267: case 272: case 277: case 282:
				case 287: case 292: case 297: case 302: case 307: case 340:
				case 345: case 350: case 355: case 360: case 365: case 370:
				case 375: case 380: case 385: case 390: case 395: case 400:
				case 405: case 410: case 415: case 420: case 425: case 430:
				case 435: case 664: case 690: case 695: case 700: case 705: 
				case 710: case 715: case 720: case 725: case 730: case 736: 
				case 741: case 748: case 753: case 758: case 763: case 768: 
				case 773: case 778: case 784: case 789: case 794: case 801: 
				case 807: case 812: case 817: case 985: case 990: case 995: 
				case 1002: case 1189: case 1194: case 1248: case 1253: case 1258: 
				case 1265: case 1269: case 1274: case 1556: case 1639: case 1684:
				case 1693: case 1702: case 1711: case 1735: case 1747: case 1756:
				case 1770: case 1779: case 1788: case 1797: case 1808: case 1847: 
				case 2091: case 2100: case 2123: case 2134: case 2143: case 2152:
				case 2161: case 2175: case 2229: case 2233: case 2237: case 2490: 
				case 2491: case 2500: case 2501: case 2502: case 2503: case 2632:
				case 2637: case 2642: case 2697: case 2703: case 2709: case 2715:
				case 2721: case 2731: case 2737: case 2743: case 2749: case 2755:
				case 2765: case 2771: case 2777: case 2783: case 2789: case 2799:
				case 2805: case 2811: case 2817: case 2823: case 2833: case 2839:
				case 2845: case 2851: case 2857: case 2867: case 2873: case 2879:
				case 2885: case 2891: case 2925: case 2930: case 2940: case 2944:
				case 2948:
				{ //ArmsArmor
					return 28;
				}

				default:
					return -1;
			}

			/*// start research with name
			if(name.IndexOf("Coif") >= 0 || name.IndexOf("coif") >= 0 
			|| name.IndexOf("Cap") >= 0 || name.IndexOf("cap") >= 0
			|| name.IndexOf("Helm") >= 0 || name.IndexOf("helm") >= 0
			|| name.IndexOf("Hat") >= 0 || name.IndexOf("hat") >= 0
			|| name.IndexOf("Crown") >= 0 || name.IndexOf("crown") >= 0
			|| name.IndexOf("Circlet") >= 0 || name.IndexOf("circlet") >= 0)
			{
				return 21;
			}
			else if(name.IndexOf("Gloves") >= 0 || name.IndexOf("gloves") >= 0 
			|| name.IndexOf("Gauntlet") >= 0 || name.IndexOf("gauntlet") >= 0
			|| name.IndexOf("Mitten") >= 0 || name.IndexOf("mitten") >= 0)
			{
				return 22;
			}
			else if(name.IndexOf("Boots") >= 0 || name.IndexOf("boots") >= 0
			|| name.IndexOf("Sabator") >= 0 || name.IndexOf("sabator") >= 0)
			{
				return 23;
			}
			else if(name.IndexOf("Vest") >= 0 || name.IndexOf("vest") >= 0 
			|| name.IndexOf("Robe") >= 0 || name.IndexOf("robe") >= 0
			|| name.IndexOf("Jerkin") >= 0 || name.IndexOf("jerkin") >= 0
			|| name.IndexOf("Breastplate") >= 0 || name.IndexOf("breastplate") >= 0
			|| name.IndexOf("Tunic") >= 0 || name.IndexOf("tunic") >= 0
			|| name.IndexOf("Frock") >= 0 || name.IndexOf("frock") >= 0
			|| name.IndexOf("Hauberk") >= 0 || name.IndexOf("hauberk") >= 0)
			{
				return 25;
			}
			else if(name.IndexOf("Leg") >= 0 || name.IndexOf("leg") >= 0 
			|| name.IndexOf("Pants") >= 0 || name.IndexOf("pant") >= 0
			|| name.IndexOf("Greave") >= 0 || name.IndexOf("greave") >= 0
			|| name.IndexOf("Chausses") >= 0 || name.IndexOf("chausses") >= 0)
			{
				return 27;
			}
			else if(name.IndexOf("Sleeves") >= 0 || name.IndexOf("sleeves") >= 0 
			|| name.IndexOf("Arms") >= 0 || name.IndexOf("arms") >= 0
			|| name.IndexOf("Spaulder") >= 0 || name.IndexOf("spaulder") >= 0)
			{
				return 28;
			}
			else
			{
				return -1;
			}*/
		}
		#endregion

		#region All skill, resist , focus bonus
		//This method translates a string to an eProperty (all available and buffable/bonusable properties on livings)
		public int NameToPropriety(string name)
		{
			if((language.GetString("strength_string")).Equals(name))		return 1;
			if(language.GetString("dexterity_string").Equals(name))			return 2; 
			if(language.GetString("constitution_string").Equals(name))		return 3;
			if(language.GetString("quickness_string").Equals(name))			return 4; 
			if(language.GetString("intelligence_string").Equals(name))		return 5;
			if(language.GetString("piety_string").Equals(name))				return 6; 
			if(language.GetString("empathy_string").Equals(name))			return 7;
			if(language.GetString("charisma_string").Equals(name))			return 8;

			if(language.GetString("power_string").Equals(name))				return 9;
			if(language.GetString("hits_string").Equals(name))				return 10;
				
			if(language.GetString("two_handed_string").Equals(name))		return 20;
			if(language.GetString("body_magic_string").Equals(name))		return 21;
			if(language.GetString("chants_string").Equals(name))			return 22;
			if(language.GetString("critical_strike_string").Equals(name))	return 23;
			if(language.GetString("crossbow_string").Equals(name))			return 24;
			if(language.GetString("crush_string").Equals(name))				return 25;
			if(language.GetString("death_servant_string").Equals(name))		return 26;
			if(language.GetString("deathsight_string").Equals(name))		return 27;
			if(language.GetString("dual_wield_string").Equals(name))		return 28;
			if(language.GetString("earth_magic_string").Equals(name))		return 29;
			if(language.GetString("enhancements_string").Equals(name))		return 30;
			if(language.GetString("envenom_string").Equals(name))			return 31;
			if(language.GetString("fire_magic_string").Equals(name))		return 32;
			if(language.GetString("flexible_string").Equals(name))			return 33;
			if(language.GetString("cold_magic_string").Equals(name))		return 34;
			if(language.GetString("instruments_string").Equals(name))		return 35;
			if(language.GetString("matter_magic_string").Equals(name))		return 37;
			if(language.GetString("longbow_string").Equals(name))			return 36;
			if(language.GetString("mind_string").Equals(name))				return 38;
			if(language.GetString("parry_string").Equals(name))				return 40;
			if(language.GetString("polearm_string").Equals(name))			return 41;
			if(language.GetString("rejuvenation_string").Equals(name))		return 42;
			if(language.GetString("shield_string").Equals(name))			return 43;
			if(language.GetString("slash_string").Equals(name))				return 44;
			if(language.GetString("smiting_string").Equals(name))			return 45;
			if(language.GetString("soulrending_string").Equals(name))		return 46;
			if(language.GetString("spirit_magic_string").Equals(name))		return 47;
			if(language.GetString("staff_string").Equals(name))				return 48;
			if(language.GetString("stealth_string").Equals(name))			return 49;
			if(language.GetString("thrust_string").Equals(name))			return 50; 
			if(language.GetString("wind_magic_string").Equals(name))		return 51; 
			if(language.GetString("sword_string").Equals(name))				return 52; 
			if(language.GetString("hammer_string").Equals(name))			return 53; 
			if(language.GetString("axe_string").Equals(name))				return 54; 
			if(language.GetString("left_axe_string").Equals(name))			return 55; 
			if(language.GetString("spear_string").Equals(name))				return 56; 
			if(language.GetString("mending_string").Equals(name))			return 57; 
			if(language.GetString("augmentation_string").Equals(name))		return 58; 
			if(language.GetString("darkness_string").Equals(name))			return 60; 
			if(language.GetString("suppression_string").Equals(name))		return 61; 
			if(language.GetString("runecarving_string").Equals(name))		return 62; 
			if(language.GetString("stormcalling_string").Equals(name))		return 63; 
			if(language.GetString("light_magic_string").Equals(name))		return 65; 
			if(language.GetString("void_magic_string").Equals(name))		return 66; 
			if(language.GetString("mana_magic_string").Equals(name))		return 67; 
			if(language.GetString("composite_bow_string").Equals(name))		return 68; 
			if(language.GetString("battlesongs_string").Equals(name))		return 69; 
			if(language.GetString("enchantments_string").Equals(name))		return 70; 
			if(language.GetString("blades_string").Equals(name))			return 72; 
			if(language.GetString("blunt_string").Equals(name))				return 73; 
			if(language.GetString("piercing_string").Equals(name))			return 74; 
			if(language.GetString("large_weaponry_string").Equals(name))	return 75; 
			if(language.GetString("mentalism_string").Equals(name))			return 76; 
			if(language.GetString("regrowth_string").Equals(name))			return 77; 
			if(language.GetString("nurture_string").Equals(name))			return 78; 
			if(language.GetString("nature_affinity_string").Equals(name))	return 79; 
			if(language.GetString("music_string").Equals(name))				return 80; 
			if(language.GetString("celtic_dual_string").Equals(name))		return 81; 
			if(language.GetString("celtic_spear_string").Equals(name))		return 82; 
			if(language.GetString("recurve_bow_string").Equals(name))		return 83; 
			if(language.GetString("valor_string").Equals(name))				return 84; 
			if(language.GetString("cave_magic_string").Equals(name))		return 85; 
			if(language.GetString("bone_army_string").Equals(name))			return 86; 
			if(language.GetString("verdant_path_string").Equals(name))		return 87; 
			if(language.GetString("creeping_path_string").Equals(name))		return 88; 
			if(language.GetString("arboreal_path_string").Equals(name))		return 89; 
			if(language.GetString("scythe_string").Equals(name))			return 90; 
			if(language.GetString("thrown_string").Equals(name))			return 91; 
			if(language.GetString("hand_to_hand_string").Equals(name))		return 92; 
			if(language.GetString("bow_string").Equals(name))				return 93; 
			if(language.GetString("pacification_string").Equals(name))		return 94; 
			if(language.GetString("summoning_string").Equals(name))			return 98;
		
			return 0;
		}

		public int FocusNameToPropriety(string name)
		{
			if(language.GetString("darkness_focus_string").Equals(name))		return 120;
			if(language.GetString("suppression_focus_string").Equals(name))		return 121;  
			if(language.GetString("runecarving_focus_string").Equals(name))		return 122;  
			if(language.GetString("spirit_magic_focus_string").Equals(name))	return 123;  
			if(language.GetString("fire_magic_focus_string").Equals(name))		return 124;  
			if(language.GetString("wind_magic_focus_string").Equals(name))		return 125;  
			if(language.GetString("cold_magic_focus_string").Equals(name))		return 126;  
			if(language.GetString("earth_magic_focus_string").Equals(name))		return 127;  
			if(language.GetString("light_magic_focus_string").Equals(name))		return 128;  
			if(language.GetString("body_magic_focus_string").Equals(name))		return 129;  
			if(language.GetString("matter_magic_focus_string").Equals(name))	return 130;  
			if(language.GetString("mind_magic_focus_string").Equals(name))		return 132;  
			if(language.GetString("void_magic_focus_string").Equals(name))		return 133;  
			if(language.GetString("mana_magic_focus_string").Equals(name))		return 134;  
			if(language.GetString("enchantments_focus_string").Equals(name))	return 135;  
			if(language.GetString("mentalism_focus_string").Equals(name))		return 136;  
			if(language.GetString("summoning_focus_string").Equals(name))		return 137;  
			if(language.GetString("bone_army_focus_string").Equals(name))		return 138;  
			if(language.GetString("painworking_focus_string").Equals(name))		return 139;  
			if(language.GetString("deathsight_focus_string").Equals(name))		return 140;  
			if(language.GetString("death_servant_focus_string").Equals(name))	return 141;  
			if(language.GetString("verdant_path_focus_string").Equals(name))	return 142;  
			if(language.GetString("creeping_path_focus_string").Equals(name))	return 143;  
			if(language.GetString("arboreal_path_focus_string").Equals(name))	return 144;

			return 0;
		}

		public int ResistNameToPropriety(string name)
		{
			if(language.GetString("body_resist_string").Equals(name))			return 11; 
			if(language.GetString("cold_resist_string").Equals(name))			return 12; 
			if(language.GetString("crush_resist_string").Equals(name))			return 13; 
			if(language.GetString("energy_resist_string").Equals(name))			return 14; 
			if(language.GetString("heat_resist_string").Equals(name))			return 15; 
			if(language.GetString("matter_resist_string").Equals(name))			return 16; 
			if(language.GetString("slash_resist_string").Equals(name))			return 17; 
			if(language.GetString("spirit_resist_string").Equals(name))			return 18; 
			if(language.GetString("thrust_resist_string").Equals(name))			return 19;

			return 0;

		}

		public string SpellTypeToDolSpellType(string name, int objectType)
		{
			if(language.GetString("damage_over_time_string").Equals(name))	return language.GetString("DamageOverTime"); 
			if(language.GetString("disease_string").Equals(name))			return language.GetString("Disease"); 
			if(language.GetString("speed_decrease_string").Equals(name))
				if(objectType == (int)eObjectType.Poison) return language.GetString("UnbreakableSpeedDecrease");
				else return language.GetString("SpeedDecrease"); 
			if(language.GetString("stat_decrease_string").Equals(name))		return language.GetString("StrengthDebuff");
			if(language.GetString("str_con_decrease_string").Equals(name))	return language.GetString("StrengthConstitutionDebuff"); 
			if(language.GetString("direct_damage_string").Equals(name))	return language.GetString("DirectDamage"); 

			return null;
		}

		public int DamageTypeNameToPropriety(string name)
		{
			if(language.GetString("body_resist_string").Equals(name))			return 10; 
			if(language.GetString("cold_resist_string").Equals(name))			return 11; 
			if(language.GetString("crush_resist_string").Equals(name))			return 1; 
			if(language.GetString("energy_resist_string").Equals(name))			return 12; 
			if(language.GetString("heat_resist_string").Equals(name))			return 13; 
			if(language.GetString("matter_resist_string").Equals(name))			return 14; 
			if(language.GetString("slash_resist_string").Equals(name))			return 2; 
			if(language.GetString("spirit_resist_string").Equals(name))			return 15; 
			if(language.GetString("thrust_resist_string").Equals(name))			return 3;

			return 0;
		}

		#endregion

		#region All dye decoding function
		public static bool IsDye(int model)
		{
			switch(model)
			{
				case 229: case 494: case 495: //all dye and remover model
				{
					return true;
				}
			}
			return false;
		}

		public int GetDyeColorFromName(string name)
		{
			if(name.IndexOf(language.GetString("mild_acid_emanel_remover_string")) >= 0 || name.IndexOf(language.GetString("cloth_bleach_string")) >= 0 || name.IndexOf(language.GetString("mild_acid_wash_string")) >= 0) // all original
			{
				return 0;
			}
			else if(name.IndexOf(language.GetString("rust_string")) >= 0 ) // all rust //49
			{
				return 49;
			}
			else if(name.IndexOf(language.GetString("black_string")) >= 0) // all black //74
			{
				return 74;
			}
			else if(name.IndexOf(language.GetString("light_blue_string")) >= 0)  // all blue //51
			{
				return 51;
			}
			else if(name.IndexOf(language.GetString("dark_blue_string")) >= 0)  //86
			{
				return 86;
			}
			else if(name.IndexOf(language.GetString("royal_blue_string")) >= 0) //53
			{
				return 53;
			}
			else if(name.IndexOf(language.GetString("blue_string")) >= 0)  // 52
			{
				return 52;
			}
			else if(name.IndexOf(language.GetString("light_turquoise_string")) >= 0) // all turquoise  //55
			{
				return 55;
			}
			else if(name.IndexOf(language.GetString("royal_turquoise_string")) >= 0) //57
			{
				return 57;
			}
			else if(name.IndexOf(language.GetString("turquoise_string")) >= 0) //56
			{
				return 56;
			}
			else if(name.IndexOf(language.GetString("light_teal_string")) >= 0) // all teal //58
			{
				return 58;
			}
			else if(name.IndexOf(language.GetString("dark_teal_string")) >= 0) //60
			{
				return 60;
			}
			else if(name.IndexOf(language.GetString("royal_teal_string")) >= 0) //59
			{
				return 59;
			}
			else if(name.IndexOf(language.GetString("teal_string")) >= 0)  //34
			{
				return 34;
			}
			else if(name.IndexOf(language.GetString("light_brown_string")) >= 0) // all brown //61
			{
				return 61;
			}
			else if(name.IndexOf(language.GetString("dark_brown_string")) >= 0)  // 63
			{
				return 63;
			}
			else if(name.IndexOf(language.GetString("brown_string")) >= 0) //62
			{
				return 62;
			}
			else if(name.IndexOf(language.GetString("light_red_string")) >= 0) // all red //64
			{
				return 64;
			}
			else if(name.IndexOf(language.GetString("royal_red_string")) >= 0) //66
			{
				return 66;
			}
			else if(name.IndexOf(language.GetString("red_string")) >= 0) //65
			{
				return 65;
			}
			else if(name.IndexOf(language.GetString("crimson_string")) >= 0) // all crimson  //67
			{
				return 67;
			}
			else if(name.IndexOf(language.GetString("light_green_string")) >= 0) // all green //68
			{
				return 68;
			}
			else if(name.IndexOf(language.GetString("royal_green_string")) >= 0) //70
			{
				return 70;
			}
			else if(name.IndexOf(language.GetString("forest_green_string")) >= 0) //71
			{
				return 71;
			}
			else if(name.IndexOf(language.GetString("green_string")) >= 0) //69
			{
				return 69;
			}
			else if(name.IndexOf(language.GetString("dark_gray_string")) >= 0) // all gray //73
			{
				return 73;
			}
			else if(name.IndexOf(language.GetString("charcoal_string")) >= 0) //all charcoal //43
			{
				return 43;
			}
			else if(name.IndexOf(language.GetString("light_orange_string")) >= 0) //all orange //75
			{
				return 75;
			}
			else if(name.IndexOf(language.GetString("royal_orange_string")) >= 0) //77
			{
				return 77;
			}
			else if(name.IndexOf(language.GetString("orange_string")) >= 0) //76
			{
				return 76;
			}
			else if(name.IndexOf(language.GetString("light_purple_string")) >= 0) //all purple //78
			{
				return 78;
			}
			else if(name.IndexOf(language.GetString("dark_purple_string")) >= 0) //87
			{
				return 87;
			}
			else if(name.IndexOf(language.GetString("royal_purple_string")) >= 0) //80
			{
				return 80;
			}
			else if(name.IndexOf(language.GetString("purple_string")) >= 0) //79
			{
				return 79;
			}
			else if(name.IndexOf(language.GetString("light_yellow_string")) >= 0) //all yellow //81
			{
				return 81;
			}
			else if(name.IndexOf(language.GetString("royal_yellow_string")) >= 0) //83
			{
				return 83;
			}
			else if(name.IndexOf(language.GetString("yellow_string")) >= 0) //82
			{
				return 82;
			}
			else if(name.IndexOf(language.GetString("violet_string")) >= 0) //all violet //84
			{
				return 84;
			}
			else if(name.IndexOf(language.GetString("mauve_string")) >= 0) //all mauve  //85
			{
				return 85;
			}

			return 0;
		}
		#endregion

		#region Horse parsing function
		
		public static bool IsSteed(string name, int model)
		{
			switch(model)
			{
				// horse model
				case 413: case 447: case 448: case 449: case 450:
				// flying steed
				case 1207: case 1235: case 1236:
					return true;
			}

			/*if(name.IndexOf("horse") >= 0 || name.IndexOf("Dragon Fly") >= 0
			|| name.IndexOf("Ampheretere") >= 0 || name.IndexOf("Gryphon") >= 0 )
				return true;*/

			return false;
		}
		#endregion

		#region Region decoding function
		/*
		public static bool IsDungeon(ushort regionId)
		{
			switch(regionId)
			{
				case 19: case 21: case 22: case 23: case 24: case 25:
				case 35: case 36: case 37: case 40: case 45: case 46:
				case 48: case 49: case 59: case 60: case 61: case 62:
				case 63: case 65: case 66: case 67: case 68: case 69: 
				case 78: case 79: case 80: case 83: case 88: case 89: 
				case 91: case 92: case 93: case 94: case 98: case 99: 
				case 123: case 124: case 125: case 126:	case 127: case 128: 
				case 129: case 135: case 136: case 137: case 140: case 145: 
				case 146: case 149: case 150: case 160: case 161: case 180: 
				case 188: case 189: case 190: case 191: case 198: case 199: 
				case 220: case 221: case 222: case 223: case 224: case 225: 
				case 226: case 227: case 228: case 229: case 230: case 233: 
				case 243: case 244: case 245: case 246: case 248: case 249: 
				case 300: case 301: case 302: case 303: case 304: case 305: 
				case 306: case 307: case 308: case 309: case 310: case 311: 
				case 312: case 313: case 314: case 315: case 316: case 317: 
				case 318: case 319: case 320: case 321: case 322: case 323: 
				case 324: case 325: case 326: case 327: case 328: case 329: 
				case 330: case 331: case 332: case 333: case 334: case 335: 
				case 345: case 346: case 347: case 348: case 349: case 350: 
				case 351: case 352: case 353: case 354: case 355: case 356:
				case 357: case 358: case 359: case 360: case 361: case 362:
				case 363: case 364: case 373: case 374: case 375: case 376:
				case 377: case 380: case 381: case 384: case 385: case 389:
				case 390: case 392: case 393: case 394: case 395: case 396: 
				case 397: case 400: case 401: case 402: case 403: case 404:
				case 405: case 406: case 407: case 408: case 409: case 410:
				case 411: case 412: case 413: case 414: case 415: case 416:
				case 417: case 418: case 419: case 420: case 421: case 422:
				case 423: case 424: case 425: case 426: case 427: case 428:
				case 429: case 430: case 433: case 434: case 435: case 436:
				case 437: case 438: case 439: case 440: case 442: case 443:
				case 451: case 452: case 453: case 454: case 455: case 456:
				case 457: case 458: case 459: case 460: case 461: case 462:
				case 463: case 464: case 465: case 466: case 467: case 468:
				case 469: case 471: case 472: case 473: case 474: case 475:
				case 479: case 480: case 483: case 484: case 487: case 488:
				case 489: case 490: case 491: case 492: case 494: case 495:
				case 496:
				{
					return true;
					break;
				}
					
			}
			return false;
		}
		*/
		#endregion

		#region Poison / Craft decoding function
		bool IsPoison(int model)
		{
			switch(model)
			{
				case 2538: case 2539: case 2540: case 2541: case 2542:
				case 2543: case 2544: case 2545: case 2546: case 2547:
				case 2548: case 2549: case 2550: //all poison model
				{
					return true;
				}
			}
			return false;
		}

		bool IsCraftMaterial(int model)
		{
			switch(model)
			{
				case 515: // feather
				case 519: // metal bars
				case 520 :// wooden boards
				case 521 :// leather square
				case 522 :// cloth square
				case 537 :// heavy thread
				case 600 :// strip
				{
					return true;
				}
			}
			return false;
		}
		#endregion

		#endregion

	}
}
