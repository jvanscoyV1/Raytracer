using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
 


namespace Raytracing {
    
    using Vector = Vector<float>;

    public abstract class Material {
        public abstract Rgba32 Intersect(Ray ray, Vector intersection, Vector normal, Shape3D obj);
    }

    public class BasicMaterial : Material {
        public Rgba32 color {get; private set;}

        public BasicMaterial() {
            color = Rgba32.Black;
        }
        public BasicMaterial(Rgba32 c) {
            color = c;
        }
        public override Rgba32 Intersect(Ray ray, Vector intersection, Vector normal, Shape3D obj) {
            return this.color;
        }
    }

    public class PhongMaterial : Material {
        public PhongIlluminationModel lightingModel { get; private set; }

        // material color variables 
        public Rgba32 diffuseColor  { get; private set; }
        public Rgba32 specularColor { get; private set; }

        // color coefficients
        public float kDiffuse  { get; private set; }
        public float kSpecular { get; private set; }
        
        // specular highlight exponent
        public float specularExponent { get; private set; }

        // color - array of 3 Rgb32 objects that set diffuse and specular color variables
        // coefficients - array of 3 float values that set diffuse and specular coefficients
        // ke - specular exponent
        public PhongMaterial(PhongIlluminationModel model, Rgba32[] color, float[] coefficents, float ke) {
            lightingModel    = model;
            diffuseColor     = color[0];
            specularColor    = color[1];
            kDiffuse         = coefficents[0];
            kSpecular        = coefficents[1];
            specularExponent = ke;
        }

        public PhongMaterial(PhongIlluminationModel model) {
            lightingModel    = model;
            diffuseColor     = Rgba32.Black;
            specularColor    = Rgba32.White;
            kDiffuse         = 1.0f;
            kSpecular        = 1.0f;
            specularExponent = 1.0f;            
        }
       
        public override Rgba32 Intersect(Ray ray, Vector intersection, Vector normal, Shape3D obj) {
            return lightingModel.Illuminate(ray, intersection, normal.Normalize(), this, obj);
        }
    }

    public class CheckerboardMaterial : Material {
        public Material material1 { get; set; }
        public Material material2 { get; set; }
        public float checksize { get; set; }

        public CheckerboardMaterial(Material m1, Material m2, float c) {
            material1 = m1;
            material2 = m2;
            checksize = c;
        }

        public Material GetMaterial(Shape3D obj, Vector<float> intersection) {
            var textureCoords = obj.GetTextureCoords(intersection);
            if(((int)((textureCoords[0]*100)) % 2 == ((int)(textureCoords[1] * 100) % 2))) {
                return material1;
            } else {
                return material2;
            }
        }

        public override Rgba32 Intersect(Ray ray, Vector<float> intersection, Vector<float> normal, Shape3D obj) {
            return GetMaterial(obj, intersection).Intersect(ray, intersection, normal, obj);
        }
    }

}