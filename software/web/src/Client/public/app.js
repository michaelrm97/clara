function toConfig(c) {
    return {
        id: c["id"],
        name: c["name"],
        formatted: JSON.stringify(c, " ", 2)
    };
}

function parseConfig(data) {
    const config = JSON.parse(data);
    return toConfig(config);
}

function parseConfigList(data) {
    const configs = JSON.parse(data);
    return configs.map(toConfig);
}

function getConfig(uri) {
    return fetch(uri);
}

function postConfig(uri, body) {
    return fetch(uri, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: body
    });
}

function putConfig(uri, body) {
    return fetch(uri, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: body
    });
}

function deleteConfig(uri) {
    return fetch(uri, {
        method: "DELETE"
    });
}

export { parseConfig, parseConfigList, getConfig, postConfig, putConfig, deleteConfig };
