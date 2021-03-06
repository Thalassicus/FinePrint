﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Contracts;
using KSP;
using KSPAchievements;

namespace FinePrint.Contracts.Parameters
{
    public class FlightWaypointParameter : ContractParameter
    {
        private Waypoint wp;
        private double minAltitude;
        private double maxAltitude;
        private CelestialBody targetBody;
        private int waypointID;
        private bool submittedWaypoint;
        private bool outerWarning;
        private double range;
        private double centerLatitude;
        private double centerLongitude;

        // Game freaks out without a default constructor. I never use it.
        public FlightWaypointParameter()
        {
            targetBody = Planetarium.fetch.Home;
            minAltitude = 0.0;
            maxAltitude = 10000.0;
            centerLatitude = 0.0;
            centerLongitude = 0.0;
            range = 10000.0;
            wp = new Waypoint();
            submittedWaypoint = false;
            outerWarning = false;
        }

        public FlightWaypointParameter(int waypointID, CelestialBody targetBody, double minAltitude, double maxAltitude, double centerLatitude, double centerLongitude, double range)
        {
            this.targetBody = targetBody;
            this.waypointID = waypointID;
            this.minAltitude = minAltitude;
            this.maxAltitude = maxAltitude;
            wp = new Waypoint();
            submittedWaypoint = false;
            outerWarning = false;
            this.range = range;
            this.centerLatitude = centerLatitude;
            this.centerLongitude = centerLongitude;
        }

        protected override string GetHashString()
        {
            return (this.Root.MissionSeed.ToString() + this.Root.DateAccepted.ToString() + this.ID);
        }

        protected override string GetTitle()
        {
            return "Fly over " + Util.generateSiteName(Root.MissionSeed + waypointID, targetBody == Planetarium.fetch.Home);
        }

        protected override string GetMessageComplete()
        {
            return "You flew over " + Util.generateSiteName(Root.MissionSeed + waypointID, targetBody == Planetarium.fetch.Home) + ".";
        }

        protected override void OnUnregister()
        {
            if (submittedWaypoint)
                WaypointManager.RemoveWaypoint(wp);
        }

        protected override void OnSave(ConfigNode node)
        {
            int bodyID = targetBody.flightGlobalsIndex;
            node.AddValue("minAltitude", minAltitude);
            node.AddValue("maxAltitude", maxAltitude);
            node.AddValue("waypointID", waypointID);
            node.AddValue("targetBody", bodyID);
            node.AddValue("centerLatitude", centerLatitude);
            node.AddValue("centerLongitude", centerLongitude);
            node.AddValue("range", range);
        }

        protected override void OnLoad(ConfigNode node)
        {
            Util.LoadNode(node, "FlightWaypointParameter", "targetBody", ref targetBody, Planetarium.fetch.Home);
            Util.LoadNode(node, "FlightWaypointParameter", "minAltitude", ref minAltitude, 0.0);
            Util.LoadNode(node, "FlightWaypointParameter", "maxAltitude", ref maxAltitude, 10000);
            Util.LoadNode(node, "FlightWaypointParameter", "waypointID", ref waypointID, 0);
            Util.LoadNode(node, "FlightWaypointParameter", "centerLatitude", ref centerLatitude, 0.0);
            Util.LoadNode(node, "FlightWaypointParameter", "centerLongitude", ref centerLongitude, 0.0);
            Util.LoadNode(node, "FlightWaypointParameter", "range", ref range, 10000);

            if (HighLogic.LoadedSceneIsFlight && this.Root.ContractState == Contract.State.Active && this.State == ParameterState.Incomplete)
            {
                wp.celestialName = targetBody.GetName();
                wp.seed = Root.MissionSeed;
                wp.id = waypointID;
                wp.RandomizeNear(centerLatitude, centerLongitude, targetBody.GetName(), range, true);
                wp.setName();
                wp.waypointType = WaypointType.PLANE;
                wp.altitude = calculateMidAltitude();
                wp.isOnSurface = true;
                wp.isNavigatable = true;
                WaypointManager.AddWaypoint(wp);
                submittedWaypoint = true;
            }

            // Load all current missions in the tracking station.
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
            {
                if (this.Root.ContractState != Contract.State.Completed)
                {
                    wp.celestialName = targetBody.GetName();
                    wp.seed = Root.MissionSeed;
                    wp.id = waypointID;
                    wp.RandomizeNear(centerLatitude, centerLongitude, targetBody.GetName(), range, true);
                    wp.setName();
                    wp.waypointType = WaypointType.PLANE;
                    wp.altitude = calculateMidAltitude();
                    wp.isOnSurface = true;
                    wp.isNavigatable = false;
                    WaypointManager.AddWaypoint(wp);
                    submittedWaypoint = true;
                }
            }
        }

        protected override void OnUpdate()
        {
            if (this.Root.ContractState == Contract.State.Active)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (FlightGlobals.ready)
                    {
                        Vessel v = FlightGlobals.ActiveVessel;
                        float distanceToWP = float.PositiveInfinity;

                        if (submittedWaypoint && v.mainBody == targetBody)
                        {
                            if (WaypointManager.Instance() != null)
							{
								// It's weird for a space program to explore our home planet. Do test flights there instead.
                                distanceToWP = WaypointManager.Instance().LateralDistanceToVessel(wp);

                                double triggerRange = FPConfig.Rover.TriggerRange;

                                if (targetBody == Planetarium.fetch.Home)
                                {
                                    triggerRange /= 2;
                                }

                                if (distanceToWP > triggerRange * 2 && outerWarning)
                                {
                                    outerWarning = false;
                                    ScreenMessages.PostScreenMessage("You are leaving the area of " + wp.tooltip + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                                }

                                if (distanceToWP <= triggerRange * 2 && !outerWarning)
								{
                                    outerWarning = true;
                                    if (targetBody == Planetarium.fetch.Home)
									{
										// Test flights are more realistic for a space agency's home base
                                        ScreenMessages.PostScreenMessage("Approaching test flight waypoint " + wp.tooltip + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                                    }
                                    else
                                    {
                                        ScreenMessages.PostScreenMessage("Approaching " + wp.tooltip + ", beginning aerial surveillance.", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                                    }
                                }

                                if (v.altitude > minAltitude && v.altitude < maxAltitude)
                                {
                                    if (distanceToWP < triggerRange)
                                    {
                                        if (targetBody == Planetarium.fetch.Home)
                                        { 
                                            ScreenMessages.PostScreenMessage("Transmitting experimental flight data near " + wp.tooltip + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT); 
                                        }
                                        else
                                        {
                                            ScreenMessages.PostScreenMessage("Transmitting aerial surveillance data on " + wp.tooltip + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                                        }
                                        wp.isExplored = true;
                                        WaypointManager.deactivateNavPoint();
                                        WaypointManager.RemoveWaypoint(wp);
                                        submittedWaypoint = false;
                                        base.SetComplete();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private double calculateMidAltitude()
        {
            return Math.Round((minAltitude + maxAltitude) / 2.0);
        }
    }
}