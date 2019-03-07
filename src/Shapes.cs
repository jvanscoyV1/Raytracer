using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Raytracing {

    using Vector = Vector<float>;
    /// The base class for all of our 3D Objects
    public abstract class Shape3D {
        public int objID {get; set;}
        public abstract bool Intersect(Ray ray, out Vector[] intersection, out Vector[] normal);
        public Material material {get; set;}
    }

    // Triangles are defined using 3 vertices: 
    //            v0
    //            /\
    //   edge01  /  \   edge02
    //          /    \
    //         /      \
    //        v1------v2
    //          edge12
    //
    public class Triangle : Shape3D {
        public Vector vertex0 {get; set;}
        public Vector vertex1 {get; set;}
        public Vector vertex2 {get; set;}
        public Vector normal {get; private set;}
        private const float kEpsilon = 0.0000001f; // constant used in intersection method

        public Triangle(Vector v0, Vector v1, Vector v2, Material m) {
            vertex0 = v0;
            vertex1 = v1;
            vertex2 = v2;
            material = m;
            normal = CalcNormal();
        }

        public Vector CalcNormal() {
            var edge01 = vertex1 - vertex0;
            var edge02 = vertex2 - vertex0;
            // var norm = (edge01).CrossProduct(edge02); 
            // return norm.Normalize();
            var N = Vector.Build.Dense(3);
            N[0] = (edge01[1] * edge02[2]) - (edge01[2] * edge02[1]);
            N[1] = (edge01[2] * edge02[0]) - (edge01[0] * edge02[2]);
            N[2] = (edge01[0] * edge02[1]) - (edge01[1] * edge02[0]);
            return N.Normalize();
        }

        public override bool Intersect(Ray ray, out Vector[] intersection, out Vector[] normal) {
            // determinant, inverse determinant
            float det, idet;
            // point of intersection
            float x, y, z;

            Vector edge01, edge02, pvec, tvec, qvec;
            intersection = new Vector[] {
                Vector.Build.Dense(3)
            };
            normal = new Vector[] {
                Vector.Build.Dense(3)
            };
            edge01 = vertex1 - vertex0;
            edge02 = vertex2 - vertex0;
            normal[0] = this.normal;

            pvec = ray.direction.CrossProduct(edge02);
            det = edge01.DotProduct(pvec);
            // no intersection if determinant is very small (or 0)
            if(det > -kEpsilon && det < kEpsilon)
                return false;

            idet = 1.0f / det;
            tvec = ray.origin - vertex0;
            y = tvec.DotProduct(pvec) * idet;
            if(y < 0 || y > 1)
                return false;

            qvec = tvec.CrossProduct(edge01);
            z = ray.direction.DotProduct(qvec) * idet;
            if(z < 0 || y + z > 1)
                return false;

            x = idet * edge02.DotProduct(qvec);
            if(x > kEpsilon) {
                intersection[0][0] = x;
                intersection[0][1] = y;
                intersection[0][2] = z;
                return true;
            } else {
                return false;
            }
        }
    }



    public class Sphere : Shape3D {
        public Vector center {get; set;}
        public float radius {get; private set;}        
        public float radius2 {get; private set;}
        public Sphere(Vector center, float radius, Material material) {
            this.center = center;
            this.radius = radius;
            this.radius2 = radius * radius;
            this.material = material;
            // System.Console.WriteLine(this.material.color);
        }

        /// Intersect (overrides base class)
        /// out argument - intersection 
        /// returns bool - true if intersecting sphere, else false 
        public override bool Intersect(Ray ray, out Vector[] intersection, out Vector[] normal) {
            intersection = new Vector[]{
                Vector.Build.Dense(3), // i0
                // Vector.Build.Dense(3), // i1
            };
            normal = new Vector[]{
                Vector.Build.Dense(3), // n0
                // Vector.Build.Dense(3), // n1
            };

            var ray_to_center = center - ray.origin;
            float tca = ray_to_center.DotProduct(ray.direction);
            // System.Console.WriteLine(tca);            
            // if(tca < 0.0f) return false; // sphere is behind camera
            
            float d2 = ray_to_center.DotProduct(ray_to_center) - (tca * tca);
            // float d2 = ray_to_center.DotProduct(ray_to_center) - tca * tca;
            if(d2 > (radius2)) return false; // NO INTERSECTION
            float t1c = (float)Math.Sqrt(radius2 - d2);
            float t0 = tca - t1c;
            float t1 = tca + t1c;

            var center_to_ray = ray.origin - center;
            float a = ray.direction.DotProduct(ray.direction);
            float b = ray.direction.DotProduct(2.0f * center_to_ray);
            float c = center_to_ray.DotProduct(center_to_ray) - (radius2);
            if(!Extensions.Quadratic(a, b, c, ref t0, ref t1)) 
                return false;

            if (t0 > t1) {
                // swap
                float tmp = t0;
                t0 = t1;
                t1 = tmp;
            }

            if (t0 < 0) {
                t0 = t1; // if t0 is negative, let's use t1 instead
                if (t0 < 0) return false; // both t0 and t1 are negative
            }

            intersection[0] = (ray.origin + (ray.direction * t0));
            Vector i0tmp = intersection[0] - center;
            normal[0] = i0tmp.Normalize();
            return true;
        }
    }

        public class Plane : Shape3D {
        public Vector center {get; set;}
        public Vector normal {get; set;}

        private Vector min_bounds;
        private Vector max_bounds;
        public Vector p0 {get; set;}
        public Vector p1 {get; set;}
        public Vector p2 {get; set;}
        public Vector p3 {get; set;}

        /*      
            Plane constructor given:
                a center point/vector, a normal vector, 
                width (in world units), height (in world units),
                and material
            
                p0_________p1
            ^    |         | 
            |    |         |
            |    |    c  --------> n
            |    |         |
            |    |_________|   
           (V)   p2        p3
                
                (H)---------->

            to find p0-3, we need to find the horizontal and vertical 
            "projection vectors" (H, V) of the plane. 
            Using those, we can calculate p0-3 w.r.t. the center 
         */                 
        public Plane(Vector center, Vector normal, float width, float height,  Material material) {
            this.center = center;
            this.normal = normal.Normalize();
            this.material = material;
            if(normal[1] == 0.0f) {
                normal[1] = 0.000001f;
            }
            // default world "up" vector
            Vector up = Vector.Build.DenseOfArray(new float[] { 0.0f, 0.0f, 1.0f});
            // normal x up = 'horizontal' vector H of the plane
            Vector H = up.CrossProduct(normal).Normalize();
            // normal x H = 'vertical' vector V of the plane
            Vector V = normal.CrossProduct(H).Normalize();

            float max_x = float.MinValue, min_x = float.MaxValue;
            float max_y = float.MinValue, min_y = float.MaxValue;
            float max_z = float.MinValue, min_z = float.MaxValue;
            min_bounds = Vector.Build.DenseOfArray(new float[]{min_x, min_y, min_z});
            max_bounds = Vector.Build.DenseOfArray(new float[]{max_x, max_y, max_z});
            
            // calculate corner points based on the center, width, height, H, and V
            p0 = center + ((width / 2) * H) + ((height / 2) * V);
            p1 = center + ((width / 2) * H) - ((height / 2) * V);
            p2 = center - ((width / 2) * H) + ((height / 2) * V);
            p3 = center - ((width / 2) * H) - ((height / 2) * V);
            MakeBounds(p0);
            MakeBounds(p1);
            MakeBounds(p2);
            MakeBounds(p3);
        }

        private void MakeBounds(Vector p) {
            if(p[0] < min_bounds[0]) min_bounds[0] = p[0];
            if(p[1] < min_bounds[1]) min_bounds[1] = p[1];
            if(p[2] < min_bounds[2]) min_bounds[2] = p[2];
            
            if(p[0] > max_bounds[0]) max_bounds[0] = p[0];
            if(p[1] > max_bounds[1]) max_bounds[1] = p[1];
            if(p[2] > max_bounds[2]) max_bounds[2] = p[2];
        }

        private bool InBounds(Vector intersect) { 
            var i_min_epsilon = intersect + (0.0001f);
            var i_max_epsilon = intersect - (0.0001f);
            bool in_x = (i_min_epsilon[0] >= min_bounds[0]) && (i_max_epsilon[0] <= max_bounds[0]);
            bool in_y = (i_min_epsilon[1] >= min_bounds[1]) && (i_max_epsilon[1] <= max_bounds[1]);
            bool in_z = (i_min_epsilon[2] >= min_bounds[2]) && (i_max_epsilon[2] <= max_bounds[2]);
            return (in_x && in_y && in_z);
        }

        public override bool Intersect(Ray ray, out Vector[] intersection, out Vector[] normal) {
            intersection = new Vector[1];
            normal = new Vector[] { this.normal };
            float denom = this.normal.DotProduct(ray.direction.Normalize());
            if((float)Math.Abs(denom) > 0.000001f) {
                Vector rayO_center = center - ray.origin;
                float d = rayO_center.DotProduct(this.normal) / denom;
                intersection[0] = ray.origin + (ray.direction.Normalize() * d);
                return (d >= 0.0f && InBounds(intersection[0]));
            }
            return false;
        }
    }
}