local Demo = {}

function Demo.render(name)
    local title = "hello, " .. name
    print(title)
    return title
end

return Demo
