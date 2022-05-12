using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region FSM
        public class Drill
        {
            #region variables
            public Program _program;
            public IDrillState currentState;
            private IDrillState nextState;
            public string currentStateName;
            public int passes = 0;
            public bool isBottom = false;
            public bool isControllable = true;
            public bool isStandby;
            

            public float drillExtent;
            public float drillMaxExtent;
            public float drillDepth;
            public float drillMaxDepth;
            public float startDepth;
            public float startExtent;
            public float nextDepth;
            public float highestPosition;
            #endregion

            #region Defaults
            //defaults
            public IDrillState defaultCurrentState;
            public float defaultDrillExtent = 0.0f;
            public float defaultDrillMaxExtent = 40.0f;
            public float defaultDrillDepth = 0.0f;
            public float defaultDrillMaxDepth = 50.0f;
            public float defaultStartDepth = 0.0f;
            public float defaultStartExtent = 0.0f;
            #endregion

            #region parameters
            //"cutting" parameters
            public string mode = "sweep";
            public bool modeToggle = true;
            public float boomVelocity = 0.275f;//ground speed v, holding this constant forces boom RPM to adjust based on current extention. Omega = v / r.
            public float spotVelocity = 0.0166f; //m/s
            public float lowerAngle;// = (float)(0.0f);    //radians
            public float upperAngle;// = (float)(Math.PI); //radians
            public float depthCut = 0.5f; //m
            public float drillRadius; //dependent on num of drills, still a parameter in the cutting process. Must be even number of drills, or the logic in the constructor makes no sense.
            public static float drillOffset = 1.5f; //m
            public static float blockLength = 2.5f; //m

            public float extentOffset = blockLength * 11.0f; //horribly hardcoded, used to calculate Omega.
            public float depthOffset = blockLength * 14.0f; //horribly hardcoded, used to calculate Omega.

            #endregion

            #region objects
            public IMyCockpit controller;
            public IMyMotorAdvancedStator boomRotor;
            public IMyMotorAdvancedStator spotRotor;
            public IMyTextPanel lcd;
            public List<IMyPistonBase> pistons = new List<IMyPistonBase>();
            public List<IMyPistonBase> pistonsForward = new List<IMyPistonBase>();
            public List<IMyPistonBase> pistonsDown = new List<IMyPistonBase>();
            public List<IMyShipDrill> drills = new List<IMyShipDrill>();
            public List<IMyCargoContainer> cargo = new List<IMyCargoContainer>();
            
            #endregion

            #region debug
            public bool setFirstState = false;
            public bool setObjects = false;
            //public bool
            //public bool
            //public bool
            #endregion

            #region lists
            public List<IDrillState> states = new List<IDrillState>();
            public List<string> modes = new List<string>();
            public List<float> drillDepths = new List<float>();
            public List<float> drillExtents = new List<float>();
            #endregion

            
            public Drill(Program program)
            {
                _program = program;
                if (currentState == null)
                {
                    if (isStandby)
                    {
                        defaultCurrentState = new StandbyState();
                    }
                    else
                    {
                        defaultCurrentState = new DepthDetectState();
                    }
                    currentState = defaultCurrentState;
                    drillMaxExtent = defaultDrillMaxExtent;
                    drillMaxDepth = defaultDrillMaxDepth;
                    setFirstState = true;
                }
                controller = program.GridTerminalSystem.GetBlockWithName("aaa_Cockpit") as IMyCockpit;
                boomRotor = program.GridTerminalSystem.GetBlockWithName("aaa_Rotor - Boom") as IMyMotorAdvancedStator;
                spotRotor = program.GridTerminalSystem.GetBlockWithName("aaa_Rotor - Spot") as IMyMotorAdvancedStator;
                lcd = program.GridTerminalSystem.GetBlockWithName("aaa_Drill display") as IMyTextPanel;
                program.GridTerminalSystem.GetBlocksOfType(drills);
                program.GridTerminalSystem.GetBlocksOfType(pistons);
                program.GridTerminalSystem.GetBlocksOfType(cargo);
                for (int i = 0; i < pistons.Count; i++)
                {
                    //pistons[i].MaxLimit = pistons[i].HighestPosition;
                    if ((Math.Abs(pistons[i].WorldMatrix.Up.X - pistons[0].WorldMatrix.Up.X) < 0.01f) && (Math.Abs(pistons[i].WorldMatrix.Up.Y - pistons[0].WorldMatrix.Up.Y) < 0.01f) && (Math.Abs(pistons[i].WorldMatrix.Up.Z - pistons[0].WorldMatrix.Up.Z) < 0.01f))
                    {
                        pistonsForward.Add(pistons[i]);
                    }
                    else
                    {
                        pistonsDown.Add(pistons[i]);
                    }

                }
                highestPosition = pistonsDown[0].HighestPosition * pistonsDown.Count();
                drillRadius = drills.Count() / 2;
                setObjects = true;
            }

            #region Calc
            public void CalcExtent()
            {
                float temp = 0.0f;
                for (int i = 0; i < pistonsForward.Count(); i++)
                {
                    temp += pistonsForward[i].CurrentPosition;
                }

                drillExtent = temp;
            }
            public void CalcMaxExtent()
            {
                float temp = 0.0f;
                for (int i = 0; i < pistonsForward.Count(); i++)
                {
                    temp += pistonsForward[i].MaxLimit;
                }
                drillMaxExtent = temp;
            }
            public void CalcDepth()
            {
                float temp = 0.0f;
                for (int i = 0; i < drill.pistonsDown.Count(); i++)
                {
                    temp += pistonsDown[i].CurrentPosition;
                }
                drillDepth = temp;
            }
            public void CalcMaxDepth()
            {
                float temp = 0.0f;
                for (int i = 0; i < drill.pistonsDown.Count(); i++)
                {
                    temp += pistonsDown[i].MaxLimit;
                }
                drillMaxDepth = temp;
            }
            #endregion

            public void Update()
            {
                if (nextState != null)
                    SetState(nextState);

                if (currentState != null)
                    currentState.OnUpdate();

                CalcExtent();
                CalcDepth();
                CalcMaxExtent();
                CalcMaxDepth();
                states.Add(currentState);
                
            }
            private void SetState(IDrillState _newState)
            {
                nextState = null;
                if (currentState != null)
                {
                    currentState.OnExit();
                }
                currentState = _newState;
                currentState.OnEnter();
            }
            public void SetNextState(IDrillState _newState)
            {
                if (_newState != null)
                {
                    nextState = _newState;
                }
            }
        }

        //start standby 
        //let user set init parameters
        //user presses run with "start"
        //does driling procedure in selected mode
        public abstract class IDrillState
        {
            public string Name;
            public abstract void OnEnter();
            public abstract void OnUpdate();
            public abstract void OnExit();
        }
        public class StandbyState : IDrillState
        {
            public override void OnEnter()
            {
                Name = "StandbyState";
                drill.isControllable = true;
                
            }
            public override void OnUpdate()
            {
                if (drill.isStandby)
                {
                    ProcessInput();
                }
                else
                {
                    drill.SetNextState(new DepthDetectState());
                }
            }
            public override void OnExit()
            {
             
            }

            private void ProcessInput()
            {
                drill.boomRotor.LowerLimitRad = float.MinValue;
                drill.boomRotor.UpperLimitRad = float.MaxValue;
                for (int i = 0; i < drill.pistons.Count(); i++)
                {
                    drill.pistons[i].MaxLimit = drill.pistons[i].HighestPosition;
                }
                

                if(drill.controller.MoveIndicator.X != 0 || drill.controller.MoveIndicator.Y != 0 || drill.controller.MoveIndicator.Z != 0)
                {
                    if(drill.controller.MoveIndicator.X == -1)
                    {
                        drill.boomRotor.TargetVelocityRad = -0.5f;
                    }
                    else if(drill.controller.MoveIndicator.X == 1)
                    {
                        drill.boomRotor.TargetVelocityRad = 0.5f;

                    }
                    //////////////////////////////////
                    if (drill.controller.MoveIndicator.Y == -1)
                    {
                        for(int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = -0.5f;
                        }
                    }
                    else if (drill.controller.MoveIndicator.Y == 1)
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = 0.5f;
                        }
                    }
                    //////////////////////////////////
                    if (drill.controller.MoveIndicator.Z == -1)
                    {
                        for (int i = 0; i < drill.pistonsForward.Count(); i++)
                        {
                            drill.pistonsForward[i].Velocity = 0.5f;
                        }
                    }
                    else if (drill.controller.MoveIndicator.Z == 1)
                    {
                        for (int i = 0; i < drill.pistonsForward.Count(); i++)
                        {
                            drill.pistonsForward[i].Velocity = -0.5f;
                        }
                    }
                }
                else
                {
                    drill.boomRotor.TargetVelocityRad = 0.0f;
                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].Velocity = 0.0f;
                    }
                    for (int i = 0; i < drill.pistonsForward.Count(); i++)
                    {
                        drill.pistonsForward[i].Velocity = 0.0f;
                    }
                }

            }
        }
        public class DepthDetectState : IDrillState
        {
            public override void OnEnter()
            {
                Name = "DepthDetectState";
                drill.isControllable = false;
            }
            public override void OnUpdate()
            {
                if (!drill.isStandby)
                {
                    if (!Detect())
                    {
                        drill.SetNextState(new InitState());
                    }
                }
                else
                {
                    drill.SetNextState(new StandbyState());
                }
                drill.CalcExtent();
                drill.CalcDepth();
                drill.CalcMaxExtent();
                drill.CalcMaxDepth();
                drill.drillExtents.Add(drill.drillExtent);
                drill.drillDepths.Add(drill.drillDepth);
            }
            public override void OnExit()
            {
                drill.states.Clear();
                drill.drillExtents.Clear();
                drill.drillDepths.Clear();
            }
            public bool Detect()
            {

                // need to set spot rotor angle to zero, or pi, -pi, in order to sweep
                drill.boomRotor.TargetVelocityRad = 0.0f;
                drill.spotRotor.UpperLimitRad = 0.0f;
                drill.spotRotor.LowerLimitRad = 0.0f;
                drill.spotRotor.TargetVelocityRad = 0.0f;
                for (int i = 0; i < drill.drills.Count(); i++)
                {
                    drill.drills[i].Enabled = false;
                }
                for (int i = 0; i < drill.pistonsForward.Count(); i++)
                {
                    drill.pistonsForward[i].MaxLimit = drill.defaultDrillMaxExtent / drill.pistonsForward.Count();
                }
                for (int i = 0; i < drill.pistonsDown.Count(); i++)
                {
                    drill.pistonsDown[i].MaxLimit = drill.defaultDrillMaxDepth / drill.pistonsDown.Count();
                    drill.pistonsDown[i].Velocity = 0.20000000000f;
                }

                if (drill.drillDepths.Count() >= 50)
                {
                    float lastPosition = drill.drillDepths[drill.drillDepths.Count() - 1];
                    float oldPosition = drill.drillDepths[drill.drillDepths.Count() - 50];
                    if (Math.Abs(lastPosition - oldPosition) < 0.0001f)
                    {
                        if (Math.Abs(lastPosition - drill.highestPosition) < 0.001f)
                        {
                            drill.startDepth = lastPosition;
                        }
                        else
                        {
                            drill.startDepth = lastPosition + drill.depthCut;
                        }

                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].MaxLimit = drill.startDepth / drill.pistonsDown.Count();
                        }
                        drill.startExtent = drill.drillExtent;
                        return false;
                    }
                }
                return true;
            }
        }
        public class InitState : IDrillState
        {
            public override void OnEnter()
            {
                Name = "InitState";
                drill.isControllable = false;
                drill.passes = 0;
            }
            public override void OnUpdate()
            {
                if (!drill.isStandby)
                {
                    switch (drill.mode)
                    {
                        case "sweep":
                            if (!SetSweep())
                            {
                                drill.SetNextState(new DrillingState());
                            }
                            break;

                        case "spot":
                            if (!SetSpot())
                            {
                                drill.SetNextState(new DrillingState());
                            }
                            break;
                    }
                }
                else
                {
                    drill.SetNextState(new StandbyState());
                }
                drill.CalcExtent();
                drill.CalcDepth();
                drill.CalcMaxExtent();
                drill.CalcMaxDepth();
            }
            public override void OnExit()
            {

            }
            private bool SetSweep()
            {
                drill.boomRotor.LowerLimitRad = drill.lowerAngle;
                drill.boomRotor.UpperLimitRad = drill.upperAngle;
                if ((Math.Abs(drill.startDepth - drill.highestPosition) < 0.01f))
                {
                    if (drill.drillDepth < 0.001f)
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = 0.0f;
                        }
                        if (Math.Abs(drill.drillExtent - drill.startExtent) < 0.01f)
                        {
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                drill.pistonsForward[i].Velocity = 0.0f;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                if(Math.Sign(drill.pistonsForward[i].CurrentPosition - drill.startExtent / drill.pistonsForward.Count()) == 1)
                                {
                                    drill.pistonsForward[i].Velocity = -0.5f;
                                }
                                else
                                {
                                    drill.pistonsForward[i].Velocity = 0.3f;

                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = -0.5f;
                        }
                    }
                }
                else
                {
                    if (Math.Abs(drill.drillDepth - drill.startDepth) < 0.001f)
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = 0.0f;
                        }
                        if (Math.Abs(drill.drillExtent - drill.startExtent) < 0.01f)
                        {
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                drill.pistonsForward[i].Velocity = 0.0f;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                drill.pistonsForward[i].Velocity = -0.5f;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = -0.5f;
                        }
                    }
                }

                if (Math.Abs(drill.boomRotor.Angle - drill.boomRotor.LowerLimitRad) < 0.005f)
                {
                    drill.boomRotor.TargetVelocityRad = 0.0f;
                    for (int i = 0; i < drill.drills.Count(); i++)
                    {
                        drill.drills[i].Enabled = true;
                    }
                    if (Math.Abs(drill.drillDepth - drill.startDepth) < 0.001f)
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = 0.0f;
                        }
                        drill.boomRotor.TargetVelocityRad = drill.boomVelocity / (drill.extentOffset + drill.drillExtent);
                        return false;
                    }
                    else
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].Velocity = 0.2f;
                        }
                    }
                }
                else
                {
                    if (drill.drillDepth < 0.001f)
                    {
                        drill.boomRotor.TargetVelocityRad = -0.05f;
                    }
                }
                return true;
            }
            private bool SetSpot()
            {
                drill.CalcExtent();
                drill.CalcDepth();
                drill.CalcMaxExtent();
                drill.CalcMaxDepth();
                if (Math.Abs(drill.drillDepth - (drill.startDepth - 3.0f)) < 0.001f)
                {
                    drill.spotRotor.UpperLimitRad = float.MaxValue;
                    drill.spotRotor.LowerLimitRad = float.MinValue;
                    drill.spotRotor.TargetVelocityRPM = 1.0f / 60.0f / drill.spotVelocity;
                    for (int i = 0; i < drill.drills.Count(); i++)
                    {
                        drill.drills[i].Enabled = true;
                    }

                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].MaxLimit = drill.highestPosition / drill.pistonsDown.Count();
                        drill.pistonsDown[i].Velocity = 0.0f;
                    }
                    return false;
                }
                else
                {
                    for(int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].MaxLimit = (drill.startDepth - 3.0f) / drill.pistonsDown.Count();
                        drill.pistonsDown[i].Velocity = -0.05f;
                    }
                }
                return true;
            }
        }
        public class DrillingState : IDrillState
        {
            public override void OnEnter()
            {
                Name = "DrillingState";
                drill.isControllable = false;
            }

            public override void OnUpdate()
            {
                if (!drill.isStandby)
                {
                    if(drill.cargo.All(x => x.GetInventory().IsFull) == true)
                    {
                        for (int i = 0; i < drill.drills.Count(); i++)
                        {
                            drill.drills[i].Enabled = false;
                        }
                        drill.isStandby = true;
                    }
                    else
                    {
                        drill.isStandby = false;
                        switch (drill.mode)
                        {
                            case "sweep":
                                if (drill.isBottom)
                                {
                                    if (!SweepMove())
                                    {
                                        drill.SetNextState(new DepthDetectState());
                                        drill.passes = 0;
                                        drill.isBottom = false;
                                    }
                                }
                                else
                                {
                                    SweepDrill();
                                }
                                break;
                            case "spot":
                                if (drill.isBottom)
                                {
                                    if (!SpotMove())
                                    {
                                        drill.isBottom = false;
                                    }
                                }
                                else
                                {
                                    SpotDrill();
                                }
                                break;
                        }
                    }

                    
                }
                else
                {
                    drill.SetNextState(new StandbyState());
                }
                
                drill.CalcExtent();
                drill.CalcDepth();
                drill.CalcMaxExtent();
                drill.CalcMaxDepth();
            }
            public override void OnExit()
            {

            }

            public bool SweepDrill()
            {
                
                if (drill.drills.All(IMyShipDrill => IMyShipDrill.Enabled == false))
                {
                    for (int i = 0; i < drill.drills.Count(); i++)
                    {
                        drill.drills[i].Enabled = true;
                    }
                }

                if (((Math.Sign(drill.boomRotor.TargetVelocityRad) == -1) && (Math.Abs(drill.boomRotor.Angle - drill.boomRotor.LowerLimitRad) < 0.01f)) || ((Math.Sign(drill.boomRotor.TargetVelocityRad) == 1) && (Math.Abs(drill.boomRotor.Angle - drill.boomRotor.UpperLimitRad) < 0.01f)) && !drill.isBottom)
                {
                    drill.boomRotor.TargetVelocityRad = 0.0f;
                    drill.nextDepth = (drill.startDepth + (drill.passes + 1) * (drill.depthCut + Drill.drillOffset));
                    drill.CalcDepth();
                    if (drill.pistonsDown.All(IMyPistonBase => Math.Abs(IMyPistonBase.CurrentPosition - IMyPistonBase.HighestPosition) < 0.001f))
                    {
                        drill.isBottom = true;
                        if (drill.pistonsForward.All(IMyPistonBase => Math.Abs(IMyPistonBase.CurrentPosition - IMyPistonBase.HighestPosition) < 0.001f))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (drill.nextDepth <= drill.highestPosition)
                        {
                            for (int i = 0; i < drill.pistonsDown.Count(); i++)
                            {
                                drill.pistonsDown[i].MaxLimit = drill.nextDepth / drill.pistonsDown.Count();
                            }
                        }
                        else
                        {
                            for (int i = 0; i < drill.pistonsDown.Count(); i++)
                            {
                                drill.pistonsDown[i].MaxLimit = drill.highestPosition / drill.pistonsDown.Count();
                            }
                        }
                        drill.CalcMaxDepth();
                        if (Math.Abs(drill.drillDepth - drill.nextDepth) <= 0.01f)
                        {
                            for (int i = 0; i < drill.pistonsDown.Count(); i++)
                            {
                                drill.pistonsDown[i].Velocity = 0.0f;
                            }
                            drill.boomRotor.TargetVelocityRad = (float)Math.Pow(-1, drill.passes + 1) * drill.boomVelocity / (drill.extentOffset + drill.drillExtent);
                            drill.passes++;
                        }
                        else
                        {
                            for (int i = 0; i < drill.pistonsDown.Count(); i++)
                            {
                                drill.pistonsDown[i].Velocity = 0.05f;
                            }
                        }

                    }
                }
                else
                {
                    drill.boomRotor.TargetVelocityRad = (float)Math.Pow(-1, drill.passes) * drill.boomVelocity / (drill.extentOffset + drill.drillExtent);
                }
                return true;
            }
            public bool SweepMove()
            {
                if (Math.Abs(drill.boomRotor.Angle - drill.boomRotor.LowerLimitRad) < 0.005f)
                {
                    drill.boomRotor.TargetVelocityRad = 0.0f;
                    return false;
                }
                else
                {
                    if (drill.drillDepth < 0.005f)
                    {
                        drill.CalcExtent();
                        drill.CalcDepth();
                        drill.CalcMaxExtent();
                        drill.CalcMaxDepth();
                        if (Math.Abs(drill.drillExtent - drill.drillMaxExtent) < 0.001f)
                        {
                            drill.boomRotor.TargetVelocityRad = -0.05f;
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                drill.pistonsForward[i].Velocity = 0.0f;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < drill.pistonsForward.Count(); i++)
                            {
                                drill.pistonsForward[i].MaxLimit = 2 * (drill.startExtent + drill.drillRadius * Drill.blockLength) / drill.pistonsForward.Count();
                                drill.pistonsForward[i].Velocity = 0.5f;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < drill.pistonsDown.Count(); i++)
                        {
                            drill.pistonsDown[i].MaxLimit = 0.0f;
                            drill.pistonsDown[i].Velocity = -0.5f;
                        }
                    }
                }
                return true;
            }

            public bool SpotDrill()
            {
                drill.boomRotor.LowerLimitRad = drill.boomRotor.Angle;
                drill.boomRotor.UpperLimitRad = drill.boomRotor.Angle;
                drill.boomRotor.TargetVelocityRad = 0.0f;

                drill.spotRotor.TargetVelocityRPM = 1.0f / 60.0f / drill.spotVelocity;
                if (drill.drills.All(IMyShipDrill => IMyShipDrill.Enabled == false))
                {
                    for (int i = 0; i < drill.drills.Count(); i++)
                    {
                        drill.drills[i].Enabled = true;
                    }
                }
                drill.CalcMaxDepth();
                if (Math.Abs(drill.drillDepth - drill.highestPosition) <= 0.01f)
                {
                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].Velocity = 0.0f;
                    }
                    drill.isBottom = true;
                    return false;
                }
                else
                {
                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].Velocity = drill.spotVelocity / drill.pistonsDown.Count();
                    }
                }

                return true;
            }
            public bool SpotMove()
            {
                if (drill.drillDepth < 0.005f)
                {
                    drill.CalcExtent();
                    drill.CalcDepth();
                    drill.CalcMaxExtent();
                    drill.CalcMaxDepth();
                    drill.spotRotor.TargetVelocityRad = 0.0f;
                    for (int i = 0; i < drill.drills.Count(); i++)
                    {
                        drill.drills[i].Enabled = false;
                    }
                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].MaxLimit = 0.0f;
                        drill.pistonsDown[i].Velocity = 0.0f;
                    }

                }
                else
                {
                    for (int i = 0; i < drill.pistonsDown.Count(); i++)
                    {
                        drill.pistonsDown[i].MaxLimit = 0.0f;
                        drill.pistonsDown[i].Velocity = -0.5f;
                    }
                }
                return true;
            }
        }
        public IDrillState ToState(string value)
        {
            switch (value)
            {
                case "DepthDetectState":
                    if (value.Contains("DepthDetectState"))
                    {
                        return new DepthDetectState();
                    }
                    return null;
                case "InitState":
                    if (value.Contains("InitState"))
                    {
                        return new InitState();
                    }
                    return null;
                case "DrillingState":
                    if (value.Contains("DrillingState"))
                    {
                        return new DrillingState();
                    }
                    return null;
                default:
                    return null;
            }
        }
        #endregion
        public MyCommandLine _commandLine = new MyCommandLine();
        public Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        MyIni storageIni = new MyIni();
        static Drill drill;

        #region commands
        public void StandbyToggle()
        {
            drill.isStandby = !drill.isStandby;
        }
        public void ModeToggle()
        {
            drill.modeToggle = !drill.modeToggle;
            switch (drill.modeToggle)
            {
                case true:
                    drill.mode = "sweep";
                    break;
                case false:
                    drill.mode = "spot";
                    break;
            }
        }
        public void SetLower()
        {
            drill.lowerAngle = drill.boomRotor.Angle;
        }
        public void SetUpper()
        {
            drill.upperAngle = drill.boomRotor.Angle;
        }
        #endregion

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            drill = new Drill(this);
            _commands["standby"] = StandbyToggle;
            _commands["mode"] = ModeToggle;
            _commands["setLower"] = SetLower;
            _commands["setUpper"] = SetUpper;


            //drill._commands["start"] = drill.Start;
            //if (string.IsNullOrEmpty(Storage))
            //{
            //    drill.currentState = drill.defaultCurrentState;
            //    drill.drillExtent = drill.defaultDrillExtent;
            //    drill.drillDepth = drill.defaultDrillDepth;
            //}
            //else
            //{
            //    drill.currentState = ToState(storageIni.Get("State", "currentState").ToString());
            //    drill.drillExtent = storageIni.Get("State", "drillExtent").ToSingle();
            //    drill.drillDepth = storageIni.Get("State", "drillDepth").ToSingle();
            //}
        }
        public void Save()
        {
            //storageIni.Set("State", "currentState", drill.currentState.ToString());
            //storageIni.Set("State", "drillExtent", drill.drillExtent.ToString());
            //storageIni.Set("State", "currentDepth", drill.drillDepth.ToString());
            //Storage = storageIni.ToString();
        }
        public void Main(string argument, UpdateType updateSource)
        {

            if (_commandLine.TryParse(argument))
            {
                Action commandStandbyToggle;

                string command = _commandLine.Argument(0);

                if (command == null)
                {

                }
                else if (_commands.TryGetValue(command, out commandStandbyToggle))
                {
                    commandStandbyToggle();
                }
                else
                {

                }
            }
            drill.Update();

            Echo("Input parameters: \n");
            drill.lcd.WriteText("Input parameters: \n");
            
            Echo("Depth of cut: " + drill.depthCut.ToString());
            drill.lcd.WriteText("Depth of cut: " + drill.depthCut.ToString() + "\n", true);
            
            Echo("Target velocity: " + drill.boomVelocity / (drill.extentOffset + drill.drillExtent));
            drill.lcd.WriteText("Target velocity: " + drill.boomVelocity / (drill.extentOffset + drill.drillExtent) + "\n", true);
            Echo("Min: " + drill.lowerAngle.ToString());
            drill.lcd.WriteText("Min: " + drill.lowerAngle.ToString() + "\n", true);
            
            Echo("Max: " + drill.upperAngle.ToString());
            drill.lcd.WriteText("Max: " + drill.upperAngle.ToString() + "\n", true);
            
            Echo("-------------------------------\n");
            drill.lcd.WriteText("-------------------------------\n", true);
            
            Echo("Monitoring...\n");
            drill.lcd.WriteText("Monitoring...\n", true);
            
            Echo("State: " + drill.currentState.ToString());
            drill.lcd.WriteText("State: " + drill.currentState.ToString() + "\n", true);
            
            Echo("Passes: " + drill.passes.ToString());
            drill.lcd.WriteText("Passes: " + drill.passes.ToString() + "\n", true);
            
            Echo("Boom velocity: " + drill.boomRotor.TargetVelocityRad.ToString());
            drill.lcd.WriteText("Boom velocity: " + drill.boomRotor.TargetVelocityRad.ToString() + "\n", true);
            
            Echo("Boom angle: " + drill.boomRotor.Angle.ToString());
            drill.lcd.WriteText("Boom angle: " + drill.boomRotor.Angle.ToString() + "\n", true);
            
            Echo("Extent: " + drill.drillExtent);
            drill.lcd.WriteText("Extent: " + drill.drillExtent + "\n", true);
            
            Echo("Depth: " + drill.drillDepth);
            drill.lcd.WriteText("Depth: " + drill.drillDepth + "\n", true);
            
            Echo("Start extent: " + drill.startExtent);
            drill.lcd.WriteText("Start extent: " + drill.startExtent + "\n", true);
            
            Echo("Start depth: " + drill.startDepth);
            drill.lcd.WriteText("Start depth: " + drill.startDepth + "\n", true);
            
            Echo("Max extent: " + drill.drillMaxExtent.ToString());
            drill.lcd.WriteText("Max extent: " + drill.drillMaxExtent.ToString() + "\n", true);
            
            Echo("Max depth: " + drill.drillMaxDepth.ToString());
            drill.lcd.WriteText("Max depth: " + drill.drillMaxDepth.ToString() + "\n", true);
            
            Echo("Distance to depth " + Math.Abs(drill.drillDepth - drill.drillMaxDepth));
            drill.lcd.WriteText("Distance to depth " + Math.Abs(drill.drillDepth - drill.drillMaxDepth) + "\n", true);
            
            Echo("Distance from minimum " + Math.Abs(drill.boomRotor.Angle - drill.boomRotor.LowerLimitRad));
            drill.lcd.WriteText("Distance from minimum " + Math.Abs(drill.boomRotor.Angle - drill.boomRotor.LowerLimitRad) + "\n", true);
            
            Echo("Distance from maximum " + Math.Abs(drill.boomRotor.Angle - drill.boomRotor.UpperLimitRad));
            drill.lcd.WriteText("Distance from maximum " + Math.Abs(drill.boomRotor.Angle - drill.boomRotor.UpperLimitRad) + "\n", true);

            Echo("---------------------");
            drill.lcd.WriteText("----------------------\n", true);

            Echo("Debug...");
            drill.lcd.WriteText("Debug...\n", true);

            Echo("isBottom: " + drill.isBottom);
            drill.lcd.WriteText("isBottom: " + drill.isBottom + "\n", true);

            Echo("setFirstState: " + drill.setFirstState);
            drill.lcd.WriteText("setFirstState: " + drill.setFirstState + "\n", true);

            if(drill.pistonsForward !=null && drill.pistonsDown != null) {
                Echo("Forward velocity: " + drill.pistonsForward[0].Velocity);
                drill.lcd.WriteText("Forward velocity: " + drill.pistonsForward[0].Velocity + "\n", true);

                Echo("Down velocity: " + drill.pistonsDown[0].Velocity);
                drill.lcd.WriteText("Down velocity: " + drill.pistonsDown[0].Velocity + "\n", true);
            }

            ///////////////////////////
            //cockpit displays
            ///////////////////////////
            //LARGE DISPLAY [0]
            //drill.controller.GetSurface(0).WriteText("First");

            //TOP LEFT SCREEN [1]
            drill.controller.GetSurface(1).WriteText("Boom velocity: " + drill.boomRotor.TargetVelocityRad.ToString() + "\n");
            drill.controller.GetSurface(1).WriteText("Boom angle: " + drill.boomRotor.Angle + "\n", true);
            drill.controller.GetSurface(1).WriteText("Lower limit: " + drill.boomRotor.LowerLimitRad + "\n", true);
            drill.controller.GetSurface(1).WriteText("Upper limit: " + drill.boomRotor.UpperLimitRad + "\n", true);

            //TOP CENTER SCREEN [2]
            drill.controller.GetSurface(2).WriteText("Extent: " + drill.drillExtent + "\n");
            drill.controller.GetSurface(2).WriteText("Depth: " + drill.drillDepth + "\n", true);
            
            drill.controller.GetSurface(2).WriteText("----------------\n", true);
            drill.controller.GetSurface(2).WriteText("Start extent: " + drill.startExtent + "\n", true);
            drill.controller.GetSurface(2).WriteText("Start depth: " + drill.startDepth + "\n", true);
            drill.controller.GetSurface(2).WriteText("Max extent: " + drill.drillMaxExtent.ToString() + "\n", true);
            drill.controller.GetSurface(2).WriteText("Max depth: " + drill.drillMaxDepth.ToString() + "\n", true);

            //TOP RIGHT SCREEN [3]
            drill.controller.GetSurface(3).WriteText("Debug...\n");
            drill.controller.GetSurface(3).WriteText("State: " + drill.currentState + "\n", true);
            drill.controller.GetSurface(3).WriteText("isStandby: " + drill.isStandby + "\n", true);
            drill.controller.GetSurface(3).WriteText("isControllable: " + drill.isControllable + "\n", true);
            drill.controller.GetSurface(3).WriteText("Mode: " + drill.mode + "\n", true);
            drill.controller.GetSurface(3).WriteText("isBottom: " + drill.isBottom + "\n", true);

            if (drill.pistonsForward != null && drill.pistonsDown != null)
            {
                drill.controller.GetSurface(3).WriteText("Forward velocity: " + drill.pistonsForward[0].Velocity + "\n", true);

                drill.controller.GetSurface(3).WriteText("Down velocity: " + drill.pistonsDown[0].Velocity + "\n", true);
            }

            //KEYBOARD [4]
            drill.controller.GetSurface(4).WriteText("Inputs: " + drill.controller.MoveIndicator);

            //NUMPAD [5]
            //drill.controller.GetSurface(0).WriteText("First");
        }






    }
}
