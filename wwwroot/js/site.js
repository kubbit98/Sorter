var dotNetObjRef;
var listenerRegistered = false;
var photoModal;
var isListenForRegisterKey = false;
function registerDNF(dotNetObject) {
    dotNetObjRef = dotNetObject;
};
function resetPosition() {
    var zoom = document.getElementById("zoom");
    if (null == zoom) return;
    zoom.style.transform = "translate(0px, 0px) scale(1)"
    loadZoom();
}
function loadZoom() {
    const ourElement = document.getElementById('photoModal');
    if (null == photoModal) photoModal = ourElement;
    else if (photoModal != ourElement) listenerRegistered = false;
    if (!listenerRegistered) {
        ourElement.addEventListener('hidden.bs.modal', function (e) {
            resetPosition();
            //console.log("hidden");
        })
        listenerRegistered = true;
        //console.log("logged");
    }
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
    var video = document.getElementById("videoTagId");
    if (null != video) {
        video.load();
        video.focus({ focusVisible: true });
    }
}
function unLoadVideo() {
    var video = document.getElementById("videoTagId");
    if (null != video) {
        video.pause();
        while (video.firstChild) {
            video.removeChild(video.firstChild);
        }
        video.load();
    }
}
function closeModal(modalId) {
    var element = document.getElementById(modalId);
    if (null != element) {
        var modal = bootstrap.Modal.getOrCreateInstance(element);
        if (null != modal) modal.hide();
    }
}
function openModal(modalId) {
    var element = document.getElementById(modalId);
    if (null != element) {
        var modal = bootstrap.Modal.getOrCreateInstance(element);
        if (null != modal) modal.show();
    }
}
function toggleModal(modalId) {
    var element = document.getElementById(modalId);
    if (null != element) {
        var modal = bootstrap.Modal.getOrCreateInstance(element);
        if (null != modal) {
            if (modal._isShown) {
                modal.hide();
            }
            else {
                modal.show();
                loadZoom();
            }
        }
    }
}
async function setFocusOnElement(elementId) {
    document.getElementById(elementId).focus({ focusVisible: true });
}
var activeButton;
async function setButtonSuccess(isTrue) {
    if (isTrue == "true") {

        if (isListenForRegisterKey) {
            activeButton.classList = ['btn btn-primary'];
        }
        else {
            isListenForRegisterKey = true;
        }
        if (document.activeElement.hasAttribute('data-keybind-button')) {
            activeButton = document.activeElement;
            activeButton.classList = ['btn btn-info'];
        }
    }
    else {
        activeButton.classList = ['btn btn-primary'];
        isListenForRegisterKey = false;
        activeButton = null;
    }
}
async function configKeyBindModal() {
    var modal = document.getElementById('keyBindsModal')
    //https://stackoverflow.com/a/49583286
    modal.addEventListener('hide.bs.modal', function (event) {
        if (isListenForRegisterKey) {
            event.preventDefault();
            setButtonSuccess(false);
        }
    })
}

window.addEventListener('keydown', function (event) {
    if (event.key == ' ' && document.activeElement.tagName != 'INPUT') {
        event.preventDefault();
    }
    if (isListenForRegisterKey) {
        if (event.key == 'Shift') {
            return;
        }
        else if (event.key == 'Alt' || event.key == 'Control') {
            setButtonSuccess(false);
            return;
        }
        dotNetObjRef.invokeMethodAsync('HandleKeyDown', event.key, isListenForRegisterKey, activeButton.title);
        setButtonSuccess(false);
    }
    else if (!document.activeElement.hasAttribute('data-prevent-keydown')) {
        dotNetObjRef.invokeMethodAsync('HandleKeyDown', event.key, isListenForRegisterKey, '');
    }
});