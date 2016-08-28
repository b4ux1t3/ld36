﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace LD36Quill18
{
    public enum InputMode { Normal, Aiming, Inventory, Looking, Fabricator }

    public class Game
    {

        public Game()
        {
            _instance = this;

            Random = new Random();
            messageLog = new List<string>();

            frameBuffer = new FrameBuffer(0, 0, Console.WindowWidth, Console.WindowHeight-1);
            LevelRenderArea = new Rect(0, 0, 80 - statAreaWidth, 24 - 3);
            //frameBufferStats = new FrameBuffer(Console.WindowWidth - statAreaWidth, 0, statAreaWidth, Console.WindowHeight);

            Map = new Map();

            SetupUpgrades();

            PlayerCharacter.UpdateVision();
            Map.CurrentFloor.Recenter(true);
        }

        public Map Map { get; protected set; }
        public FabricatorUpgrade[] FabricatorUpgrades { get; set; }

        static public Game Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new Game();
                }

                return _instance;
            }
        }

        public PlayerCharacter PlayerCharacter { get; set; }

        public Random Random;

        static private Game _instance;

        #region bookkeeping
        private bool exit = false;
        private bool doTick = false;
        public AimingOverlay aimingOverlay;
        public LookingOverlay lookingOverlay;
        #endregion

        private int statAreaWidth = 16;

        public Rect LevelRenderArea;
           
        private FrameBuffer frameBuffer;
        //private FrameBuffer frameBufferStats;

        public InputMode InputMode = InputMode.Normal;

        private List<string> messageLog;

        public int SleepFor { get; set; }

        private void SetupUpgrades()
        {
            FabricatorUpgrades = new FabricatorUpgrade[] {
                new FabricatorUpgrade("Improved Impact Dampening (+10 Max Health)", (pc) => { pc.MaxHealth += 10; pc.Health += 10; }),
                new FabricatorUpgrade("Add External Battery Pack (+0.0050% Max Energy)", (pc) => { pc.MaxEnergy += 50; pc.Energy += 50; }),
                new FabricatorUpgrade("Upgrade Sensors (+1 Vision Range)", (pc) => { pc.VisionRadius += 1; }),
                new FabricatorUpgrade("Add Armor Plating (+1 Damage Reduction)", (pc) => { pc.DamageReduction += 1; }),
                new FabricatorUpgrade("Servo Actuator Replacement (+1 to Dodging)", (pc) => { pc.DodgeBonus += 1; }),
                new FabricatorUpgrade("Targetting CPU Over-Clocking (+1 to Accuracy)", (pc) => { pc.ToHitBonus += 1; }),
                new FabricatorUpgrade("Myomer Strengthening (+1 Melee Damage)", (pc) => { pc.MeleeDamage += 1; }),
                new FabricatorUpgrade("Kinetic Accelerator Boost (+1 Ranged Damage)", (pc) => { pc.RangedDamage += 1; }),
};
        }

        /// <summary>
        /// Called non-stop by the main program loop
        /// </summary>
        public bool Update()
        {
            //RedrawRequests.Clear();

            if (ClearRequested)
            {
                ClearRequested = false;
                FrameBuffer.Instance.Clear();
            }

            KeyboardHandler.Update_Keyboard();

            if (InputMode == InputMode.Inventory)
            {
                DrawInventoryScreen();
            }
            else if (InputMode == InputMode.Fabricator)
            {
                DrawFabricatorScreen();
            }
            else
            {
                Map.CurrentFloor.Update(doTick);
                doTick = false;

                if (aimingOverlay != null)
                {
                    aimingOverlay.Draw();
                }
                if (lookingOverlay != null)
                {
                    lookingOverlay.Draw();
                }

                PrintStats();

            }

            PrintMessages();
            frameBuffer.DrawFrame();

            if(exit || Restart)
            {
                return false;
            }

            if (SleepFor > 0)
            {
                Thread.Sleep(SleepFor);
                SleepFor = 0;
            }

            return true;
        }

        public bool Restart = false;

        void PrintMessages()
        {
            int numLines = messageLog.Count;
            if (numLines > 3)
            {
                numLines = 3;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

            for (int i = 0; i < numLines; i++)
            {
                Console.SetCursorPosition(0, Console.WindowHeight - 1 - i);
                Console.Write( messageLog[ messageLog.Count - 1 - i ].PadRight( FrameBuffer.Instance.Width - statAreaWidth - 1) );
            }
        }

        public void Message(string m)
        {
            string[] ms = m.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries );

            foreach( string m2 in ms)
                messageLog.Add(m2);
        }

        public void DebugMessage(string m)
        {
            // check debug flag
            Message("[DEBUG] " + m);
        }

        public void DoTick()
        {
            doTick = true;
        }

        public void ExitGame()
        {
            exit = true;
        }


        void PrintStats()
        {
            int x = FrameBuffer.Instance.Width - statAreaWidth - 1;
            int y = 0;

            // Blank the area first
            while (y < Console.WindowHeight)
            {
                //Console.SetCursorPosition(x, y);
                frameBuffer.Write(x, y, "\u2551                ");
                y++;
            }


            y = 0;
            x++;
            
            if (PlayerCharacter != null)
            {
                //Console.Write("HP: {0}/{1}", PlayerCharacter.Health, PlayerCharacter.MaxHealth);
                frameBuffer.Write(x, y, "  == STATS ==   ");
                y++;
                frameBuffer.Write(x, y, string.Format("HP: {0}/{1}", PlayerCharacter.Health, PlayerCharacter.MaxHealth));
                y++;
                frameBuffer.Write(x, y, string.Format("Energy: 0.{0}%", PlayerCharacter.Energy.ToString("d4")));
                y++;
                frameBuffer.Write(x, y, string.Format("Scraps: ${0}", PlayerCharacter.Money));
                y++;


            }

            if (lookingOverlay != null)
            {
                PrintLookingInfo(Map.CurrentFloor.GetTile(lookingOverlay.X, lookingOverlay.Y));
            }
            else if (aimingOverlay != null)
            {
                PrintLookingInfo(Map.CurrentFloor.GetTile(aimingOverlay.X, aimingOverlay.Y));
            }


            PrintHotkeys();
        }

        void PrintLookingInfo(Tile tile)
        {
            int x = FrameBuffer.Instance.Width - statAreaWidth - 1;
            int y = 8;
            frameBuffer.Write(x, y, "\u255F");
            for (int i = 1; i < statAreaWidth; i++)
            {
                frameBuffer.Write(x + i, y, "\u2500");

            }

            y++;
            y++;

            if (tile != null && tile.WasSeen == true)
            {
                frameBuffer.Write(x + 2, y, tile.TileType.ToString());

                y++;

                if (tile.Character != null)
                {
                    frameBuffer.Write(x + 2, y, Utility.WordWrap(tile.Character.Name, statAreaWidth));
                }

                y++;

                if (tile.Item != null)
                {
                    frameBuffer.Write(x + 2, y, Utility.WordWrap(tile.Item.Name, statAreaWidth));
                }

                y++;
                y++;
            }
            else {
                y++;
                y++;
                frameBuffer.Write(x + 2, y,"No Sensor\nCoverage");
                y++;
                y++;
            }

            frameBuffer.Write(x, y, "\u255F");
            for (int i = 1; i < statAreaWidth; i++)
            {
                frameBuffer.Write(x + i, y, "\u2500");

            }

        }

        void PrintHotkeys()
        {
            int x = FrameBuffer.Instance.Width - statAreaWidth + 1;
            int y = FrameBuffer.Instance.Height - 6;

            frameBuffer.Write(x, y, "[F]ire");
            y++;
            frameBuffer.Write(x, y, "[I]nventory");
            y++;
            frameBuffer.Write(x, y, "[L]ook");
            y++;
            frameBuffer.Write(x, y, "< Go Up");
            y++;
            frameBuffer.Write(x, y, "> Go Down");
            y++;
            frameBuffer.Write(x, y, "[CTRL-Q]uit");
            y++;

        }

        public bool ClearRequested = false;

        void DrawFabricatorScreen()
        {
            FrameBuffer.Instance.Write(5, 0, "              -- Augmentation Station --");
            FrameBuffer.Instance.Write(5, 1, "Improving near-scrap-metal with scrap-metal since 2092!");
            FrameBuffer.Instance.Write(5, 2, "           (Permanently upgrade a statistic.)");
            FrameBuffer.Instance.Write(5, 4, "               Your Metal Scraps: $" + PlayerCharacter.Instance.Money);



            int y = 8;

            for (int i = 0; i < FabricatorUpgrades.Length; i++)
            {
                char c = (char)((int)'a' + i);
                FrameBuffer.Instance.Write(5, y++, 
                       c +") [$"+FabricatorUpgrades[i].NextUpgradeCost.ToString()+"] " + FabricatorUpgrades[i].Name
                      );
            }

        }

        void DrawInventoryScreen()
        {
            FrameBuffer.Instance.Write(30, 0, "-- Inventory --");
            if (KeyboardHandler.inventoryExamineMode)
            {
                FrameBuffer.Instance.Write(20, 1, "Hit [?] to Stop Examining");
            }
            else
            {
                FrameBuffer.Instance.Write(20, 1, "Hit [?] to Examine Items");
            }

            if (KeyboardHandler.inventoryEquippedMode)
            {
                FrameBuffer.Instance.Write(20, 2, "Hit [TAB] to See Inventory");
                for (int i = 0; i < PlayerCharacter.EquippedItems.Length; i++)
                {
                    int x = 5;
                    int y = i;
                    y += 5;

                    string name = "";
                    if (PlayerCharacter.Instance.EquippedItems[i] != null)
                        name = PlayerCharacter.Instance.EquippedItems[i].Name;

                    char c = (char)((int)'a' + i);

                    FrameBuffer.Instance.Write(x, y, string.Format("[{0}] {2}: {1}", c, name, Enum.GetNames(typeof(EquipSlot))[i]));
                }
            }
            else
            {
                FrameBuffer.Instance.Write(20, 2, "Hit [TAB] to See Equiped");
                for (int i = 0; i < PlayerCharacter.Instance.Items.Length; i++)
                {
                    bool col1 = i < PlayerCharacter.Instance.Items.Length / 2;
                    int x = col1 ? 5 : 35;
                    int y = col1 ? i : i - PlayerCharacter.Instance.Items.Length / 2;
                    y += 5;

                    string name = "";
                    if (PlayerCharacter.Instance.Items[i] != null)
                        name = PlayerCharacter.Instance.Items[i].Name;

                    char c = (char)((int)'a' + i);

                    FrameBuffer.Instance.Write(x, y, string.Format("[{0}] {1}", c, name));
                }
            }
        }

        public void RestartGame()
        {
            Restart = true;
        }

        public void Victory()
        {
            VictoryScreen vs = new VictoryScreen();

            Console.WriteLine("");
            Console.WriteLine("");

            while (true)
            {
                Console.WriteLine("Abort, Retry, Ignore?");
                string s = Console.ReadLine();
                if (s.ToLower() == "abort" || s.ToLower() == "a")
                {
                    ExitGame();
                    return;
                }
                if (s.ToLower() == "retry" || s.ToLower() == "r")
                {
                    RestartGame();
                    return;
                }
            }
        }

        public void BSOD()
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();

            Console.WriteLine("A problem has been detected and Warbot 9000 has been shut down to prevent damage to your facility.");
            Console.WriteLine("");
            Console.WriteLine("STRUCTURAL_INTEGRITY_FAILURE");
            Console.WriteLine("");
            Console.WriteLine("If this is the first time you've seen this error screen,");
            Console.WriteLine("restart your Warbot. If this screen appears again, follow");
            Console.WriteLine("these steps:");
            Console.WriteLine("");
            Console.WriteLine("Check to make sure any new hardware or software is properly installed.");
            Console.WriteLine("If this is a new installation, ask your military supplier for any Warbot");
            Console.WriteLine("updates you might need.");
            Console.WriteLine("");
            Console.WriteLine("If problems continue, cease all invasion plans immediately and begin to");
            Console.WriteLine("purge your military command structure. Disable BIOS memory options such as");
            Console.WriteLine("caching or biological weaponry.");
            Console.WriteLine("");
            Console.WriteLine("");

            Thread.Sleep(500);
            Console.WriteLine("Not ready reading drive A");
            Console.CursorVisible = true;

            while (true)
            {
                Console.WriteLine("Abort, Retry, Ignore?");
                string s = Console.ReadLine();
                if (s.ToLower() == "abort" || s.ToLower() == "a")
                {
                    ExitGame();
                    return;
                }
                if (s.ToLower() == "retry" || s.ToLower() == "r")
                {
                    RestartGame();
                    return;
                }
            }
        }
    }
}

