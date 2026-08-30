$l = New-Object System.Net.HttpListener
$l.Prefixes.Add("http://127.0.0.1:8899/")
$l.Start()
while ($l.IsListening) {
    try {
        $ctx = $l.GetContext()
        $resp = $ctx.Response
        $resp.StatusCode = 200
        $resp.ContentType = "text/event-stream"
        # JSON string content: backslash must appear doubled, \n = newline escape
        $c1 = 'Euler formula: $e^{i\\pi}+1=0$, vector $\\boldsymbol{v}$ and $\\bm{F}$. **Key steps**:'
        $c2 = '\n\n$$\\int_0^1 x^2 \\, dx = \\frac{1}{3}$$'
        $c3 = '\n\n- item one\n- item two'
        $body = "data: {`"choices`":[{`"delta`":{`"content`":`"$c1`"}}]}`n`n"
        $body += "data: {`"choices`":[{`"delta`":{`"content`":`"$c2`"}}]}`n`n"
        $body += "data: {`"choices`":[{`"delta`":{`"content`":`"$c3`"}}]}`n`n"
        $body += "data: [DONE]`n`n"
        $buf = [System.Text.Encoding]::UTF8.GetBytes($body)
        $resp.OutputStream.Write($buf, 0, $buf.Length)
        $resp.OutputStream.Close()
    } catch { break }
}
