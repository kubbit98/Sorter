function resetPosition() {
    document.getElementById("zoom").style.transform = "translate(0px, 0px) scale(1)";
    loadZoom();
}
function loadZoom() {
    document.getElementById('photoModal').addEventListener('hidden.bs.modal', function (e) {
        resetPosition();
    })   
    var scale = 1,
        scaleStep = 1.4,
        minZoom = 1,
        maxZoom = 5,
        panning = false,
        pointX = 0,
        pointY = 0,
        start = { x: 0, y: 0 },
        zoom = document.getElementById("zoom");
    function setTransform() {
        zoom.style.transform = "translate(" + pointX + "px, " + pointY + "px) scale(" + scale + ")";
    }
    zoom.onmouseleave = function (e) {
        panning = false;
    }
    zoom.onmousedown = function (e) {
        e.preventDefault();
        //console.log(e.clientX + " " + e.clientY);
        //console.log(e.offsetX + " " + e.offsetY);
        start = { x: e.clientX - pointX, y: e.clientY - pointY };
        panning = true;
    }
    zoom.onmouseup = function (e) {
        panning = false;
    }
    function amendStartPoint() {
        if (pointX > 0) pointX = 0;
        if (pointY > 0) pointY = 0;
        if (pointX < -zoom.clientWidth * scale + zoom.clientWidth) pointX = -zoom.clientWidth * scale + zoom.clientWidth;
        if (pointY < -zoom.clientHeight * scale + zoom.clientHeight) pointY = -zoom.clientHeight * scale + zoom.clientHeight;
    }
    zoom.onmousemove = function (e) {
        e.preventDefault();
        if (!panning) return;
        pointX = (e.clientX - start.x);
        pointY = (e.clientY - start.y);
        amendStartPoint();
        setTransform();
    }
    zoom.onwheel = function (e) {
        e.preventDefault();
        var xs = (e.offsetX - pointX) / scale,
            ys = (e.offsetY - pointY) / scale;
        if (e.wheelDelta > 0) {
            scale *= scaleStep
            if (scale >= maxZoom) scale = maxZoom;
        }
        else {
            scale /= scaleStep
            if (scale <= minZoom) scale = 1;
        }
        pointX = e.offsetX - xs * scale;
        pointY = e.offsetY - ys * scale;
        amendStartPoint();
        setTransform();
    }
}
function loadVideo() {
    document.getElementById("videoTagId")?.load();
}
function closeModal(modal) {
    var modal = bootstrap.Modal.getInstance(document.getElementById(modal))
    modal.hide();
}
function openModal(modalId) {
    var modal = new bootstrap.Modal(document.getElementById(modalId))
    modal.show();
}