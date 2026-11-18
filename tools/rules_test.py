#!/usr/bin/env python3
"""Testes de regras de negocio do Ebenezer (rodam contra qualquer abordagem de persistencia).

Uso: python3 rules_test.py http://localhost:5101 saida.json
"""
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = sys.argv[1].rstrip("/")
OUT = sys.argv[2] if len(sys.argv) > 2 else None
RESULTS = []


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as resp:
            raw = resp.read().decode() or "null"
            return resp.status, safe_json(raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode() or "null"
        return e.code, safe_json(raw)
    except Exception as e:  # noqa: BLE001
        return 0, {"_error": repr(e)}


def safe_json(raw):
    try:
        return json.loads(raw)
    except Exception:  # noqa: BLE001
        return {"_raw": raw[:200]}


def data_of(js):
    """A API pode embrulhar em {data: ...}; devolve o payload util."""
    if isinstance(js, dict) and "data" in js and ("success" in js):
        return js["data"]
    return js


def items_of(js):
    d = data_of(js)
    if isinstance(d, dict) and "items" in d:
        return d["items"] or []
    if isinstance(d, list):
        return d
    return []


def check(name, cond, detail=""):
    RESULTS.append({"check": name, "pass": bool(cond), "detail": str(detail)[:300]})
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else "  -> " + str(detail)[:300]), flush=True)


def ids(items):
    return {i.get("id") for i in items}


def cat_ids(prayer_js):
    d = data_of(prayer_js)
    return {c.get("id") for c in (d.get("categories") or [])} if isinstance(d, dict) else set()


def uname(prefix):
    return (prefix + uuid.uuid4().hex)[:18]


class User:
    def __init__(self, prefix):
        self.name = uname(prefix)
        st, js = call("POST", "/auth/register", body={"userName": self.name, "email": self.name + "@t.dev", "password": "Teste@123"})
        d = data_of(js)
        self.token = d.get("token") if isinstance(d, dict) else None
        if st not in (200, 201) or not self.token:
            raise SystemExit(f"registro falhou ({st}): {js}")
        call("POST", "/profile/new", self.token, {"fullName": "Full " + prefix, "bio": "bio", "phone": "11999999999"})
        st, js = call("GET", "/profile/" + self.name, self.token)
        self.id = data_of(js).get("id")


def friend(a, b):
    st, js = call("POST", "/friendships/request", a.token, {"toUserId": b.id})
    assert st in (200, 201), (st, js)
    st, js = call("GET", "/friendships/requests/to-me?page=1&pageSize=50", b.token)
    req = [i for i in items_of(js) if i.get("requesterId") == a.id or i.get("RequesterId") == a.id]
    assert req, ("pedido nao encontrado", js)
    st, js = call("POST", f"/friendships/{req[0]['id']}/accept", b.token)
    assert st == 200, (st, js)


def new_cat(u, name, public):
    st, js = call("POST", "/categories/new", u.token, {"name": name, "description": "d", "colorHex": "#112233", "isPublic": public})
    return data_of(js).get("id") if st in (200, 201) else None


def new_prayer(u, content, public, cats=None):
    st, js = call("POST", "/prayers/new", u.token, {"content": content, "isPublic": public, "categoryIds": cats or []})
    return st, (data_of(js).get("id") if st in (200, 201) and isinstance(data_of(js), dict) else None)


def search(u, **q):
    params = {"Pagination.Page": 1, "Pagination.PageSize": 50}
    params.update(q)
    qs = urllib.parse.urlencode(params)
    return call("GET", "/prayers/search?" + qs, u.token if u else None)


def timeline(u):
    st, js = call("GET", "/prayers/timeline?page=1&pageSize=50", u.token)
    return items_of(js)


def main():
    A, B, C, D, E, F = (User(p) for p in ("autor", "amigo", "estranho", "pendente", "amigodoamigo", "amigodopendente"))

    friend(A, B)              # A <-> B aceita
    friend(B, E)              # B <-> E aceita (E e amigo do amigo de A)
    friend(D, F)              # D <-> F aceita
    st, _ = call("POST", "/friendships/request", D.token, {"toUserId": A.id})   # D -> A pendente
    check("setup: pedido pendente D->A criado", st in (200, 201), st)

    catPub = new_cat(A, "Beta publica", True)
    catPriv = new_cat(A, "alpha privada", False)
    catC = new_cat(C, "do estranho privada", False)
    catBpub = new_cat(B, "do amigo publica", True)
    check("setup: categorias criadas", all([catPub, catPriv, catC, catBpub]), (catPub, catPriv, catC, catBpub))

    # ---- Categorias: validacao ao criar oracao
    st, _ = new_prayer(A, "usa categoria privada de outro", True, [catC])
    check("categoria privada de OUTRO usuario e rejeitada na criacao", st in (404, 400), st)
    st, _ = new_prayer(A, "usa categoria publica de outro", True, [catBpub])
    check("categoria publica de outro usuario e aceita na criacao", st in (200, 201), st)
    st, _ = new_prayer(A, "ids duplicados", True, [catPub, catPub])
    check("ids de categoria duplicados sao rejeitados", st in (404, 400), st)

    st, p1 = new_prayer(A, "oracao publica texto-unico-xyz", True, [catPub])
    st2, p2 = new_prayer(A, "oracao privada", False, [])
    st3, p3 = new_prayer(A, "oracao publica com categoria privada", True, [catPriv])
    check("setup: oracoes criadas", all([p1, p2, p3]), (st, st2, st3))

    # ---- Visibilidade (GET por id)
    g = lambda u, pid: call("GET", "/prayers/" + pid, u.token if u else None)  # noqa: E731
    check("autor ve a propria oracao publica", g(A, p1)[0] == 200)
    check("autor ve a propria oracao privada", g(A, p2)[0] == 200)
    check("amigo ve oracao PUBLICA do autor", g(B, p1)[0] == 200)
    check("amigo NAO ve oracao PRIVADA do autor", g(B, p2)[0] == 404, g(B, p2)[0])
    check("estranho NAO ve oracao publica de quem nao e amigo", g(C, p1)[0] == 404, g(C, p1)[0])
    check("amizade pendente NAO da acesso", g(D, p1)[0] == 404, g(D, p1)[0])
    check("anonimo NAO ve oracao publica", g(None, p1)[0] == 404, g(None, p1)[0])

    # ---- Visibilidade (busca)
    ids_b = ids(items_of(search(B)[1]))
    check("busca do amigo traz publicas do autor", {p1, p3} <= ids_b, ids_b)
    check("busca do amigo NAO traz a privada", p2 not in ids_b, ids_b)
    ids_c = ids(items_of(search(C)[1]))
    check("busca do estranho NAO traz nada do autor", not ({p1, p2, p3} & ids_c), ids_c)
    check("busca anonima e vazia", items_of(search(None)[1]) == [], search(None)[0])
    ids_a = ids(items_of(search(A, AuthorUserName=A.name)[1]))
    check("autor busca por si mesmo e ve as 3 (inclusive privada)", {p1, p2, p3} <= ids_a, ids_a)
    ids_b_auth = ids(items_of(search(B, AuthorUserName=A.name)[1]))
    check("amigo busca por autor: so publicas", {p1, p3} <= ids_b_auth and p2 not in ids_b_auth, ids_b_auth)
    check("estranho busca por autor: nada", not ids(items_of(search(C, AuthorUserName=A.name)[1])))
    check("busca por texto", p1 in ids(items_of(search(B, Text="texto-unico-xyz")[1])))
    ids_cat = ids(items_of(search(B, CategoryId=catPub)[1]))
    check("busca por categoria publica", p1 in ids_cat, ids_cat)
    check("busca por categoria PRIVADA de outro usuario e vazia", not ids(items_of(search(B, CategoryId=catPriv)[1])))

    # ---- Exposicao de categorias
    check("amigo NAO ve a categoria privada do autor", catPriv not in cat_ids(g(B, p3)[1]), cat_ids(g(B, p3)[1]))
    check("autor ve a propria categoria privada", catPriv in cat_ids(g(A, p3)[1]), cat_ids(g(A, p3)[1]))

    # ---- Timeline
    tl_b = ids(timeline(B))
    check("timeline do amigo traz publicas do autor", {p1, p3} <= tl_b, tl_b)
    check("timeline do amigo NAO traz privada", p2 not in tl_b, tl_b)
    check("timeline do estranho nao traz nada do autor", not ({p1, p2, p3} & ids(timeline(C))))
    check("timeline de amizade pendente nao traz nada", not ({p1, p2, p3} & ids(timeline(D))))
    check("timeline do autor sem reacao NAO traz as proprias", not ({p1, p2, p3} & ids(timeline(A))), ids(timeline(A)))

    # ---- Reacao de apoio
    st, _ = call("POST", f"/prayers/{p1}/support", B.token)
    check("amigo apoia oracao publica", st in (200, 204), st)
    call("POST", f"/prayers/{p1}/support", B.token)   # idempotente
    tl_a = {i["id"]: i for i in timeline(A)}
    check("timeline do autor traz a propria oracao reagida", p1 in tl_a, list(tl_a))
    inter = (tl_a.get(p1) or {}).get("interactions") or []
    check("apoio e idempotente (1 interacao apos 2 chamadas)", len(inter) == 1, len(inter))
    check("interacao traz tipo e usuario", inter and inter[0].get("type") == "Support" and (inter[0].get("user") or {}).get("userName") == B.name, inter[:1])
    check("timeline: oracao privada do autor continua fora", p2 not in tl_a)
    call("POST", f"/prayers/{p2}/support", A.token)   # apoiar a propria privada nao vale
    check("apoio em oracao PRIVADA nao cria interacao", p2 not in {i["id"] for i in timeline(A)})
    call("POST", f"/prayers/{p1}/support-remove", B.token)
    check("remover apoio tira a oracao da timeline do autor", p1 not in {i["id"] for i in timeline(A)})

    # ---- Atualizacao de oracao
    st, _ = call("PUT", f"/prayers/{p1}", C.token, {"content": "invasor", "isPublic": True, "categoryIds": []})
    check("estranho NAO atualiza oracao alheia", st in (404, 403), st)
    check("tentativa invalida NAO apaga as categorias da oracao", catPub in cat_ids(g(A, p1)[1]), cat_ids(g(A, p1)[1]))
    st, _ = call("PUT", f"/prayers/{p1}", A.token, {"content": "novo", "isPublic": True, "categoryIds": [catBpub]})
    check("atualizacao so aceita categorias do proprio dono", st in (404, 400), st)
    st, _ = call("PUT", f"/prayers/{p1}", A.token, {"content": "novo texto", "isPublic": True, "categoryIds": [catPub, catPriv]})
    check("atualizacao com categorias proprias funciona", st == 200, st)
    check("categorias da atualizacao ficaram", {catPub, catPriv} <= cat_ids(g(A, p1)[1]), cat_ids(g(A, p1)[1]))
    call("PUT", f"/prayers/{p1}", A.token, {"content": "novo texto", "isPublic": True, "categoryIds": [catPub]})
    check("remover categoria na atualizacao", cat_ids(g(A, p1)[1]) == {catPub}, cat_ids(g(A, p1)[1]))

    # ---- Comentarios
    st, js = call("POST", f"/comments/prayer/{p1}", B.token, {"content": "primeiro"})
    cid = (data_of(js).get("comment") or {}).get("id") if st in (200, 201) else None
    check("comentario criado", bool(cid), (st, js))
    st, js = call("POST", f"/comments/prayer/{'0' * 24}", B.token, {"content": "orfao"})
    check("comentar em oracao inexistente e rejeitado", st >= 400, st)
    st, js = call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)
    items = items_of(js)
    check("listagem traz o comentario", cid in ids(items), (st, js))
    st, js = call("POST", f"/comments/comment/{cid}", A.token, {"content": "resposta"})
    sub = (data_of(js).get("comment") or {}).get("id") if st in (200, 201) else None
    check("resposta a comentario criada", bool(sub), st)
    items = items_of(call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)[1])
    cnt = [i.get("commentCount") for i in items if i.get("id") == cid]
    check("contagem de respostas diretas = 1", cnt == [1], cnt)
    st, _ = call("POST", f"/comments/{cid}/support", A.token)
    items = items_of(call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)[1])
    rb = [i.get("reactedBy") for i in items if i.get("id") == cid]
    check("reacao no comentario aparece em reactedBy (nome de usuario)", rb and A.name in (rb[0] or []), rb)
    call("POST", f"/comments/{cid}/remove-support", A.token)
    items = items_of(call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)[1])
    rb = [i.get("reactedBy") for i in items if i.get("id") == cid]
    check("remover reacao limpa reactedBy", rb and not rb[0], rb)
    st, _ = call("DELETE", f"/comments/{cid}", C.token)
    check("estranho NAO apaga comentario alheio", st in (403, 404), st)
    st, _ = call("DELETE", f"/comments/{sub}", A.token)
    check("autor apaga a propria resposta", st == 204, st)
    items = items_of(call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)[1])
    cnt = [i.get("commentCount") for i in items if i.get("id") == cid]
    check("contagem de respostas ignora excluidas", cnt == [0], cnt)
    st, _ = call("DELETE", f"/comments/{cid}", B.token)
    check("autor apaga o proprio comentario", st == 204, st)
    check("comentario excluido some da listagem", cid not in ids(items_of(call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)[1])))
    st, js = call("POST", f"/comments/comment/{cid}", A.token, {"content": "em excluido"})
    check("nao se comenta comentario excluido", st >= 400, st)

    # ---- Categorias: ordem e exclusao
    names = [c.get("name") for c in (data_of(call("GET", "/categories/mine", A.token)[1]) or [])]
    check("categorias ordenadas sem diferenciar maiusculas", names == sorted(names, key=lambda s: s.lower()), names)
    st, _ = call("DELETE", f"/categories/{catPriv}", A.token)
    check("categoria excluida", st in (200, 204), st)
    check("oracoes deixam de referenciar categoria excluida", catPriv not in cat_ids(g(A, p3)[1]), cat_ids(g(A, p3)[1]))

    # ---- Amizades: sugestoes
    sug = items_of(call("GET", "/friendships/suggestions?page=1&pageSize=50", A.token)[1])
    sug_ids = {i.get("id") for i in sug}
    check("sugestao: amigo do amigo aceito aparece", E.id in sug_ids, sug_ids)
    check("sugestao: NAO sugere via pedido pendente", F.id not in sug_ids, sug_ids)
    mutual = items_of(call("GET", f"/friendships/my-mutual-friends-with/{E.id}?page=1&pageSize=50", A.token)[1])
    check("amigos em comum A x E = [B]", [m.get("id") for m in mutual] == [B.id], mutual)

    # ---- Retrospectiva (so formato; categorias vem das orcoes do autor)
    st, js = call("GET", "/retrospective/my-retrospective", A.token)
    check("retrospectiva responde 200", st == 200, (st, str(js)[:200]))
    cats = (data_of(js) or {}).get("metrics", {}) if isinstance(data_of(js), dict) else {}
    RESULTS.append({"check": "info: retrospectiva", "pass": True, "detail": json.dumps(data_of(js))[:400]})

    # ---- Exclusao de oracao
    st, _ = call("DELETE", f"/prayers/{p1}", A.token)
    check("autor exclui oracao", st == 204, st)
    check("oracao excluida nao e mais visivel", g(A, p1)[0] == 404)
    st, js = call("GET", f"/comments/prayer/{p1}?page=1&pageSize=10", B.token)
    check("comentarios de oracao excluida nao sao listados", items_of(js) == [], (st, js))


if __name__ == "__main__":
    t0 = time.time()
    try:
        main()
    except AssertionError as e:
        check("EXECUCAO ABORTADA (assert)", False, e)
    except SystemExit as e:
        check("EXECUCAO ABORTADA", False, e)
    passed = sum(1 for r in RESULTS if r["pass"])
    print(f"\nRESUMO {BASE}: {passed}/{len(RESULTS)} ok em {time.time()-t0:.1f}s")
    if OUT:
        with open(OUT, "w", encoding="utf-8") as f:
            json.dump(RESULTS, f, ensure_ascii=False, indent=1)
    sys.exit(0 if passed == len(RESULTS) else 1)
