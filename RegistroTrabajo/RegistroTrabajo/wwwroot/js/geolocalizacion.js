// Ubicación del dispositivo, solo cuando la empresa la exige para registrar asistencia.
// Requiere un contexto seguro (HTTPS o localhost). Nunca lanza: devuelve { ok, lat, lon, error }.
window.rtGeo = {
    obtener: function () {
        return new Promise(function (resolve) {
            if (!window.isSecureContext) {
                resolve({ ok: false, error: "La ubicación solo está disponible en una conexión segura (HTTPS)." });
                return;
            }
            if (!navigator.geolocation) {
                resolve({ ok: false, error: "Este dispositivo no permite obtener la ubicación." });
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (p) { resolve({ ok: true, lat: p.coords.latitude, lon: p.coords.longitude }); },
                function (e) {
                    var m = e.code === 1 ? "No autorizó el acceso a la ubicación. Habilítelo en el navegador para registrar asistencia."
                        : e.code === 3 ? "Se agotó el tiempo para obtener la ubicación. Intente de nuevo."
                        : "No fue posible obtener la ubicación.";
                    resolve({ ok: false, error: m });
                },
                { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
        });
    }
};
