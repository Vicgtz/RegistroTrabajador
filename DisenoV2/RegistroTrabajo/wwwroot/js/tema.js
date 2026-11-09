// Aplica los colores de la empresa como variables CSS. Solo cambia la apariencia: no toca controles ni reglas de seguridad. Nunca lanza.
window.rtTema = {
    _hex: /^#[0-9A-Fa-f]{6}$/,
    _oscurecer: function (hex, factor) {
        var n = parseInt(hex.substring(1), 16);
        var c = function (v) { return Math.max(0, Math.min(255, Math.round(v * factor))); };
        return "#" + [c(n >> 16 & 255), c(n >> 8 & 255), c(n & 255)].map(function (v) { return v.toString(16).padStart(2, "0"); }).join("");
    },
    // ---- Modo claro / oscuro: se guarda en el navegador; sin preferencia guardada sigue al sistema ----
    modo: function () { return document.documentElement.getAttribute("data-bs-theme") === "dark" ? "oscuro" : "claro"; },
    establecer: function (m) {
        try {
            var oscuro = m === "oscuro" || (m !== "claro" && window.matchMedia("(prefers-color-scheme: dark)").matches);
            document.documentElement.setAttribute("data-bs-theme", oscuro ? "dark" : "light");
            if (m === "oscuro" || m === "claro") localStorage.setItem("rt-modo", m); else localStorage.removeItem("rt-modo");
        } catch (e) { }
    },
    alternar: function () { this.establecer(this.modo() === "oscuro" ? "claro" : "oscuro"); },
    _lum: function (hex) {
        var n = parseInt(hex.substring(1), 16);
        var f = function (v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
        return 0.2126 * f(n >> 16 & 255) + 0.7152 * f(n >> 8 & 255) + 0.0722 * f(n & 255);
    },
    // Aclara el color de la empresa hacia blanco hasta tener contraste >= 4.5 sobre el fondo oscuro (para texto y enlaces).
    _acentoOscuro: function (hex) {
        var n = parseInt(hex.substring(1), 16), rgb = [n >> 16 & 255, n >> 8 & 255, n & 255], fondo = this._lum("#121b2e");
        for (var t = 0; t <= 1.0001; t += 0.05) {
            var h = "#" + rgb.map(function (v) { return Math.round(v + (255 - v) * t).toString(16).padStart(2, "0"); }).join("");
            if ((this._lum(h) + 0.05) / (fondo + 0.05) >= 4.5) return h;
        }
        return "#ffffff";
    },
    aplicar: function (primario, secundario, nombre) {
        try {
            var raiz = document.documentElement.style;
            if (primario && this._hex.test(primario)) {
                raiz.setProperty("--rt-azul", primario);
                raiz.setProperty("--rt-azul-oscuro", this._oscurecer(primario, 0.45));
                raiz.setProperty("--rt-azul-claro", primario + "1A");
                raiz.setProperty("--rt-acento-oscuro", this._acentoOscuro(primario));
            } else {
                raiz.removeProperty("--rt-azul"); raiz.removeProperty("--rt-azul-oscuro"); raiz.removeProperty("--rt-azul-claro"); raiz.removeProperty("--rt-acento-oscuro");
            }
            if (secundario && this._hex.test(secundario)) raiz.setProperty("--rt-secundario", secundario);
            else raiz.removeProperty("--rt-secundario");
            if (nombre) document.title = nombre;
        } catch (e) { }
    }
};
// Si el usuario no eligió modo, acompaña los cambios del sistema operativo.
try {
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", function () {
        if (!localStorage.getItem("rt-modo")) window.rtTema.establecer(null);
    });
} catch (e) { }
