#extension GL_OES_EGL_image_external : require
precision mediump float;

/* // grey
uniform samplerExternalOES u_TextureY;

varying vec2 v_TexCoord;

void main() {
    float y = texture2D(u_TextureY, v_TexCoord).r;
    gl_FragColor = vec4(y, y, y, 1.0);
}*/


/*// color converter (not working)
uniform samplerExternalOES u_TextureY;
uniform sampler2D u_TextureU;
uniform sampler2D u_TextureV;

varying vec2 v_TexCoord;

void main() {
    float y = texture2D(u_TextureY, v_TexCoord).r;
    float u = texture2D(u_TextureU, v_TexCoord).r - 0.5;
    float v = texture2D(u_TextureV, v_TexCoord).r - 0.5;

    float r = y + 1.13983 * v;
    float g = y - 0.39465 * u - 0.58060 * v;
    float b = y + 2.03211 * u;

    gl_FragColor = vec4(r, g, b, 0.5);
}
*/

// original

uniform sampler2D u_Texture;

uniform vec4 u_LightingParameters;
uniform vec4 u_MaterialParameters;

varying vec3 v_ViewPosition;
varying vec3 v_ViewNormal;
varying vec2 v_TexCoord;

void main() {
    // We support approximate sRGB gamma.
    const float kGamma = 0.4545454;
    const float kInverseGamma = 2.2;

    // Unpack lighting and material parameters for better naming.
    vec3 viewLightDirection = u_LightingParameters.xyz;
    float lightIntensity = u_LightingParameters.w;

    float materialAmbient = u_MaterialParameters.x;
    float materialDiffuse = u_MaterialParameters.y;
    float materialSpecular = u_MaterialParameters.z;
    float materialSpecularPower = u_MaterialParameters.w;

    // Normalize varying parameters, because they are linearly interpolated in the vertex shader.
    vec3 viewFragmentDirection = normalize(v_ViewPosition);
    vec3 viewNormal = normalize(v_ViewNormal);

    // Apply inverse SRGB gamma to the texture before making lighting calculations.
    // Flip the y-texture coordinate to address the texture from top-left.
    vec4 objectColor = texture2D(u_Texture, vec2(v_TexCoord.x, 1.0 - v_TexCoord.y));
    objectColor.rgb = pow(objectColor.rgb, vec3(kInverseGamma));

    // Ambient light is unaffected by the light intensity.
    float ambient = materialAmbient;

    // Approximate a hemisphere light (not a harsh directional light).
    float diffuse = lightIntensity * materialDiffuse *
            0.5 * (dot(viewNormal, viewLightDirection) + 1.0);

    // Compute specular light.
    vec3 reflectedLightDirection = reflect(viewLightDirection, viewNormal);
    float specularStrength = max(0.0, dot(viewFragmentDirection, reflectedLightDirection));
    float specular = lightIntensity * materialSpecular *
            pow(specularStrength, materialSpecularPower);

    // Apply SRGB gamma before writing the fragment color.
    gl_FragColor.a = objectColor.a;
    gl_FragColor.rgb = pow(objectColor.rgb * (ambient + diffuse) + specular, vec3(kGamma));
}
