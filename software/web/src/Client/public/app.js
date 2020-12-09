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

export { parseConfig, parseConfigList };