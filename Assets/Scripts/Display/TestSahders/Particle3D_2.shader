Shader "Instanced/GridTestParticleShader" {
	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
		_DensityRange ("Density Range", Range(0,1.0)) = 0.5
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			#pragma surface surf Standard addshadow fullforwardshadows
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup

			sampler2D _MainTex;
			sampler2D ColourMap;
			float _DensityRange;

			struct Input {
				float2 uv_MainTex;
				float4 colour;
				float3 worldPos;
			};

			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				//StructuredBuffer<Particle> _particlesBuffer;
				StructuredBuffer<float3> Positions;
				StructuredBuffer<float2> Densities;
			#endif

			SamplerState linear_clamp_sampler;
			float scale;
			float3 colour;

			void setup()
			{
				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
					//float3 pos = _particlesBuffer[unity_InstanceID].position;
					//float size = _size;
					float3 pos = Positions[unity_InstanceID];

					unity_ObjectToWorld._11_21_31_41 = float4(scale, 0, 0, 0);
					unity_ObjectToWorld._12_22_32_42 = float4(0, scale, 0, 0);
					unity_ObjectToWorld._13_23_33_43 = float4(0, 0, scale, 0);
					unity_ObjectToWorld._14_24_34_44 = float4(pos, 1);
					unity_WorldToObject = unity_ObjectToWorld;
					unity_WorldToObject._14_24_34 *= -1;
					unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
				#endif
			}

			half _Glossiness;
			half _Metallic;
			
			//Modificado
			void surf(Input IN, inout SurfaceOutputStandard o) {

				#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				float densityValue = Densities[unity_InstanceID].x;  // Usando el primer valor de densidad
				float normalizedDensity = densityValue / _DensityRange;
				float4 sampledColor = tex2D(ColourMap, float2(normalizedDensity, 0.5));

				o.Albedo = sampledColor.rgb;
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = sampledColor.a;
				#endif
			}
			
			//original
			/*
			void surf(Input IN, inout SurfaceOutputStandard o) {
				o.Albedo = IN.colour;
				o.Metallic = 0;
				o.Smoothness = 0;
				o.Alpha = 1;
			}*/
			ENDCG
		}
			FallBack "Diffuse"
}