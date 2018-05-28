﻿using System;
using System.Collections.Generic;
using System.Threading;
using FrockRaytracer.Objects;
using FrockRaytracer.Objects.Primitives;
using FrockRaytracer.Structs;
using FrockRaytracer.Utils;
using OpenTK;
using OpenTK.Graphics.ES11;

namespace FrockRaytracer
{
    public class Raytracer
    {
        private World world;
        private MultiResolutionRaster raster;
        private DebugData ddat = new DebugData();
        private List<Thread> Threads = new List<Thread>();
        private uint WorkID = 0;
        //private uint 

        public void MaintainThreads()
        {
            bool hasThreads = Threads.Count > 0;
            for (int i = Threads.Count-1; i >= 0; --i) {
                if(!Threads[i].IsAlive) Threads.RemoveAt(i);
            }
            
            if(hasThreads && Threads.Count == 0) DebugRenderer.DebugDraw(ddat, raster.CurrentRaster, world);
        }

        /// <summary>
        /// Render everythign to raster. It is important that world stays the same
        /// </summary>
        /// <param name="_world"></param>
        /// <param name="_raster"></param>
        /// <param name="async"></param>
        public void Raytrace(World _world, MultiResolutionRaster _raster)
        {
            if(!_world.Changed && (_raster.CurrentLevel == _raster.MaxLevel || Threads.Count > 0)) return;
            if(!_world.Changed) _raster.SwitchLevel(_raster.CurrentLevel + 1, true);
            if(_world.Changed) _raster.SwitchLevel(0, false);
            _world.Changed = false;
            world = _world;
            raster = _raster;
            ddat.Clear();
            world.Cache();
            ++WorkID; // Increment work id to let the threads know they have gulag waiting for them
            Threads.Clear();

            if (Settings.IsMultithread) {
                var ConcurrentWorkerCount = Environment.ProcessorCount;
                var rpt = raster.Height / ConcurrentWorkerCount;

                for (int i = 0; i < ConcurrentWorkerCount; ++i) {
                    var i1 = i;
                    Threads.Add( new Thread(() => RenderRows(i1 * rpt, (i1 + 1) * rpt, WorkID)));
                    Threads[i].Start();
                }
                
                if (!Settings.IsAsync) {
                    foreach (var t in Threads) t.Join();
                    Threads.Clear();
                    DebugRenderer.DebugDraw(ddat, raster.CurrentRaster, world);
                }
            } else {
                RenderRows(0, raster.Height, WorkID);
            }
        }

        /// <summary>
        /// Render a specified y range
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="workID"></param>
        protected void RenderRows(int start, int end, uint workID)
        {
            bool is_debug_row = false;
            int debug_column = Settings.RaytraceDebugFrequency;
            int debug_row = (int) (Settings.RaytraceDebugRow * raster.Height);

            for (int y = start; y < end; ++y) {
                if (y == debug_row) is_debug_row = true;

                for (int x = 0; x < raster.CurrentRaster.WidthHalf; ++x) {
                    // Two workid checkers with a "choke point inbetween" to keep threads from writing over eachother
                    // Bit bad and should lock pixel area instead but yep. Mb later
                    if(workID != WorkID) return; 
                    bool debug = is_debug_row && x == debug_column;
                    if (debug) debug_column += Settings.RaytraceDebugFrequency;
                    
                    float wt = (float) x / raster.CurrentRaster.WidthHalf;
                    float ht = (float) y / raster.Height;
                    
                    Vector3 onPlane = world.Camera.FOVPlane.PointAt(wt, ht);
                    onPlane.Normalize();
                   
                    var color = traceRay(new Ray(world.Camera.Position, onPlane), debug);
                    
                    if(workID != WorkID) return;
                    raster.CurrentRaster.setPixel(x, y, color);
                }
            }
        }
        
        /// <summary>
        /// Trace the ray and go around the world in 80ns. Or more :S
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        protected Vector3 traceRay(Ray ray, bool debug = false)
        {
            Vector3 ret = Vector3.Zero;
            if (ray.Depth > Settings.MaxDepth) return ret;
            
            // Check for intersections
            RayHit hit = intersect(ray);
            if (hit.Obj == null) return world.Environent == null ? Vector3.Zero : world.Environent.GetColor(ray);
            if (debug) ddat.PrimaryRays.Add(new Tuple<Ray, RayHit>(ray, hit));
            
            // Calculate color and specular highlights. Pure mirrors dont have diffuse
            Vector3 specular = Vector3.Zero;
            Vector3 color = Vector3.Zero;
            if (!hit.Obj.Material.IsMirror) {
                color += illuminate(ray, hit, ref specular, debug);
                color += world.Environent.AmbientLight;
            }
            
            // Different materials are handled differently. Would be cool to move that into material
            if (hit.Obj.Material.IsMirror) {
                ret = traceRay(RayTrans.Reflect(ray, hit), debug);
            } else if (hit.Obj.Material.IsDielectic) {
                var n1 = ray.isOutside() ? Constants.LIGHT_IOR : hit.Obj.Material.RefractionIndex; // TODO: shorten this shizzle into a func
                var n2 = ray.isOutside() ? hit.Obj.Material.RefractionIndex : Constants.LIGHT_IOR;
                float reflect_multiplier = QuickMaths.Fresnel(n1, n2, hit.Normal, ray.Direction);
                reflect_multiplier = hit.Obj.Material.Reflectivity + (1f - hit.Obj.Material.Reflectivity) * reflect_multiplier;
                float transmission_multiplier = 1 - reflect_multiplier;

                // Reflect if its worth it
                if (reflect_multiplier > Constants.EPSILON)
                {
                    Ray reflRay = RayTrans.Reflect(ray, hit);
                    for(int i = 0; i < Settings.MaxReflectionSamples; i++)
                    {
                        Ray localRay = reflRay;
                        localRay.Direction = RRandom.RandomChange(reflRay.Direction, hit.Obj.Material.Roughness);
                        ret += reflect_multiplier * traceRay(localRay, debug);
                    }
                    ret /= Settings.MaxReflectionSamples;
                }
                if (transmission_multiplier > Constants.EPSILON) {
                    if(hit.Obj.Material.IsRefractive) {
                        var tmp_ray = RayTrans.Refract(ray, hit);
                        if(!float.IsNaN(tmp_ray.Direction.X)) // Happens rarely
                            ret += transmission_multiplier * traceRayInside(tmp_ray, hit.Obj, debug);
                    } else {
                        ret += transmission_multiplier * color;
                    }
                }
            } else {
                // Standard diffuse
                ret = color;
            }

            return ret + specular;
        }

        /// <summary>
        /// Traces the ray if its inside an object which means its probably in refraction loop
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="obj"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        protected Vector3 traceRayInside(Ray ray, Primitive obj, bool debug)
        {
            if (ray.isOutside()) return traceRay(ray, debug); // Should not happen anyway
            Vector3 ret = Vector3.Zero;
            float multiplier = 1;
            float absorb_distance = 0;

            // Do the refraction loop. Aka refract, reflect, repeat
            RayHit hit = RayHit.Default();
            while (ray.Depth < Settings.MaxDepth - 1 && multiplier > Constants.EPSILON) {
                if (!obj.Intersect(ray, ref hit)) return ret; // Should not happen either
                if(debug) ddat.RefractRays.Add(new Tuple<Ray, RayHit>(ray, hit));

                // Beer absorption
                absorb_distance += hit.T;
                var absorb = QuickMaths.Exp(-obj.Material.Absorb * absorb_distance);

                // Fresnel
                var reflect_multiplier = QuickMaths.Fresnel(obj.Material.RefractionIndex, Constants.LIGHT_IOR, ray.Direction, hit.Normal);
                var refract_multiplier = 1f - reflect_multiplier;

                // refract if its worth
                if (refract_multiplier > Constants.EPSILON)
                    ret += traceRay(RayTrans.Refract(ray, hit), debug) * refract_multiplier * multiplier * absorb;

                ray = RayTrans.Reflect(ray, hit);
                multiplier *= reflect_multiplier;
            }

            return ret;
        }

        /// <summary>
        /// Get diffuse color and specular for the hit point with a specific light
        /// </summary>
        /// <param name="light"></param>
        /// <param name="ray"></param>
        /// <param name="hit"></param>
        /// <param name="specular"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        protected Vector3 illuminateBy(Vector3 ppos, Light light, Ray ray, RayHit hit, ref Vector3 specular, bool debug)
        {
            // Calclulate if we are within light range
            var light_vec = ppos - hit.Position;
            float dist_sq = Vector3.Dot(light_vec, light_vec);
            float intens_sq = light.LightCache.IntensitySq;
            if (dist_sq >= intens_sq * Constants.LIGHT_DECAY) return Vector3.Zero;
            
            // Create a shadow ray
            var dist = (float)Math.Sqrt(dist_sq);
            light_vec = light_vec / dist;
            var shadow_ray = new Ray(hit.Position + (light_vec * Constants.EPSILON), light_vec);

            var tmp = RayHit.Default();
            foreach (var o in world.Objects) {
                if (o.Intersect(shadow_ray, ref tmp) && tmp.T < dist) {
                    if (debug) ddat.ShadowRaysOccluded.Add(new Tuple<Ray, RayHit>(shadow_ray, tmp));
                    return new Vector3();
                } 
            }

            // Diffuse
            var materialColor = hit.Obj.Material.Texture == null
                ? hit.Obj.Material.Diffuse
                : hit.Obj.Material.Texture.GetColor(hit.Obj.GetUV(hit));
            var color = materialColor * Math.Max(0.0f, Vector3.Dot(hit.Normal, light_vec));

            // Inverse square law
            var light_powah = light.Intensity / (Constants.PI4 * dist_sq);
            light_powah *= light.AngleEnergy(light_vec.Normalized());

            // Specular will be used separately
            if (hit.Obj.Material.IsGlossy) {
                var hardness = Math.Max(.0f, Vector3.Dot(-ray.Direction, QuickMaths.Reflect(-light_vec, hit.Normal)));
                specular += light.Colour * light_powah * hit.Obj.Material.Specular *
                    (float)Math.Pow(hardness, hit.Obj.Material.Shinyness);
            }

            if (debug) ddat.ShadowRays.Add(new Tuple<Ray, Vector3>(shadow_ray, ppos));
            return light.Colour * color * light_powah;
        }
       
        /// <summary>
        /// Get diffuse color and specular for the hit point
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="hit"></param>
        /// <param name="specular"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        protected Vector3 illuminate(Ray ray, RayHit hit, ref Vector3 specular, bool debug)
        {
            var ret = world.Environent.AmbientLight;
            foreach (var light in world.Lights)
            {
                var lPoints = light.GetPoints(Settings.MaxLightSamples, Settings.LSM);
                var localEnergy = Vector3.Zero;
                bool first = true;
                foreach(var lp in lPoints)
                {
                    localEnergy += illuminateBy(lp, light, ray, hit, ref specular, debug && first);
                    first = false;
                }
                localEnergy /= lPoints.Length;
                ret += localEnergy;
            }
            return ret;
        }
        
        protected RayHit intersect(Ray ray)
        {
            var hit = RayHit.Default();
            foreach (var o in world.Objects) o.Intersect(ray, ref hit);
            return hit;
        }
    }
}