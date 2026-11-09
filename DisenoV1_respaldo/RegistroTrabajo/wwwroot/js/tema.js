// Aplica los colores de la empresa como variables CSS. Solo cambia la apariencia: no toca controles ni reglas de seguridad. Nunca lanza.
window.rtTema = {
    _hex: /^#[0-9A-Fa-f]{6}$/,
    _oscurecer: function (hex, factor) {
        var n = parseInt(hex.substring(1), 16);
        var c = function (v) { return Math.max(0, Math.min(255, Math.round(v * factor))); };
        return "#" + [c(n >> 16 & 255), c(n >> 8 & 255), c(n & 255)].map(function (v) { return v.toString(16).padStart(2, "0"); }).join("");
    },
    aplicar: function (primario, secundario, nombre) {
        try {
            var raiz = document.documentElement.style;
            if (primario && this._hex.test(primario)) {
                raiz.setProperty("--rt-azul", primario);
                raiz.setProperty("--rt-azul-oscuro", this._oscurecer(primario, 0.45));
                raiz.setProperty("--rt-azul-claro", primario + "1A");
            } else {
                raiz.removeProperty("--rt-azul"); raiz.removeProperty("--rt-azul-oscuro"); raiz.removeProperty("--rt-azul-claro");
            }
            if (secundario && this._hex.test(secundario)) raiz.setProperty("--rt-secundario", secundario);
            else raiz.removeProperty("--rt-secundario");
            if (nombre) document.title = nombre;
        } catch (e) { }
    }
};
