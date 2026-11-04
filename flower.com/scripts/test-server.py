from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer


class TestHTTPServer(ThreadingHTTPServer):
    request_queue_size = 64
    daemon_threads = True


TestHTTPServer(("127.0.0.1", 4173), SimpleHTTPRequestHandler).serve_forever()
