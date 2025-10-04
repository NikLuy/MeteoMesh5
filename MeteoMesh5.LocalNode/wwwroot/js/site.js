window.ShowToastr = (type, message) => {
    if (type === "success") {
        toastr.success(message, 'Success', { timeOut: 5000 });
    }
    if (type === "error") {
        toastr.error(message, 'Error', { timeOut: 5000 });
    }
    if (type === "warning") {
        toastr.warning(message, 'Warning', { timeOut: 5000 });
    }
}

function saveAsFile(filename, bytesBase64) {
    var link = document.createElement('a');
    link.download = filename;
    link.href = "data:application/octet-stream;base64," + bytesBase64;
    document.body.appendChild(link); // Needed for Firefox
    link.click();
    document.body.removeChild(link);
}