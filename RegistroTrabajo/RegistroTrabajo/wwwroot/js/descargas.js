// Entrega al navegador un archivo generado por la API (reportes en Excel o PDF). Nunca lanza.
window.rtDescarga = {
    guardar: function (nombre, tipo, bytes) {
        try {
            var blob = new Blob([bytes], { type: tipo });
            var url = URL.createObjectURL(blob);
            var a = document.createElement("a");
            a.href = url;
            a.download = nombre;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
            return true;
        } catch (e) {
            return false;
        }
    }
};
