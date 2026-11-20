-- Monitoring and analytics (roadmap 0021): guarded pg_stat_statements, the roles for postgres-exporter and Grafana, and read-only aggregate views.
-- Both roles are NOLOGIN here; the operator runs ALTER ROLE ... LOGIN PASSWORD once (see deployment.md), so no secret is ever in the repo.
-- The views carry no user ids, e-mail addresses, tokens, IPs or status notes.

DO
$$
    BEGIN
        -- Needs shared_preload_libraries (DB restart). Without it the migration must stay green for self-hosters.
        IF current_setting('shared_preload_libraries') ~ '(^|[,\s"])pg_stat_statements([,\s"]|$)' THEN
            CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
        ELSE
            RAISE NOTICE 'pg_stat_statements skipped: not in shared_preload_libraries';
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'uw_monitor') THEN
            CREATE ROLE uw_monitor NOLOGIN;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'uw_grafana') THEN
            CREATE ROLE uw_grafana NOLOGIN;
        END IF;
    END
$$;

GRANT pg_monitor TO uw_monitor;

CREATE SCHEMA IF NOT EXISTS monitoring;

CREATE OR REPLACE VIEW monitoring."AgentCostDaily" AS
SELECT date_trunc('day', r."CreatedAt")::date AS day,
       r."Profile"                            AS profile,
       r."Model"                              AS model,
       count(*)                               AS runs,
       sum(r."InputTokens")                   AS input_tokens,
       sum(r."CachedInputTokens")             AS cached_input_tokens,
       sum(r."OutputTokens")                  AS output_tokens,
       sum(r."CostUsd")                       AS cost_usd
FROM "unshackled-word"."AgentRuns" r
GROUP BY 1, 2, 3;

CREATE OR REPLACE VIEW monitoring."AgentRunOutcomes" AS
SELECT r."Id"          AS id,
       r."Status"      AS status,
       r."Profile"     AS profile,
       r."Model"       AS model,
       r."CreatedAt"   AS created_at,
       r."FinishedAt"  AS finished_at,
       r."CostUsd"     AS cost_usd,
       r."MaxCostUsd"  AS max_cost_usd
FROM "unshackled-word"."AgentRuns" r;

CREATE OR REPLACE VIEW monitoring."ProposalStatusDaily" AS
SELECT date_trunc('day', p."CreatedAt")::date AS day,
       p."Language"                           AS language,
       p."Status"                             AS status,
       count(*)                               AS count
FROM "unshackled-word"."AgentVerseProposals" p
GROUP BY 1, 2, 3;

CREATE OR REPLACE VIEW monitoring."MappingAuthorship" AS
WITH ai AS (SELECT DISTINCT "AiUserId" AS id FROM "unshackled-word"."AgentRuns"),
     links AS (SELECT 'greek' AS language, "HebRefId", "CreatedByUserId" FROM "unshackled-word"."Elb1871GreekMapping"
               UNION ALL
               SELECT 'hebrew', "HebRefId", "CreatedByUserId" FROM "unshackled-word"."Elb1871HebrewMapping")
SELECT l.language,
       b."Name"                                                  AS book,
       count(*) FILTER (WHERE ai.id IS NOT NULL)                 AS ai_links,
       count(*) FILTER (WHERE ai.id IS NULL)                     AS human_links
FROM links l
         JOIN "unshackled-word"."BibleBooks" b ON b."Id" = l."HebRefId" / 1000000
         LEFT JOIN ai ON ai.id = l."CreatedByUserId"
GROUP BY l.language, b."Id", b."Name";

CREATE OR REPLACE VIEW monitoring."RevisionActivityDaily" AS
SELECT date_trunc('day', a."At")::date AS day,
       a."Action"                      AS action,
       count(*)                        AS count
FROM "unshackled-word"."RevisionVerseAudit" a
GROUP BY 1, 2;

-- uw_grafana: only the monitoring views, nothing in the app schema.
REVOKE ALL ON SCHEMA "unshackled-word" FROM uw_grafana;
REVOKE ALL ON ALL TABLES IN SCHEMA "unshackled-word" FROM uw_grafana;
REVOKE ALL ON SCHEMA public FROM uw_grafana;
GRANT USAGE ON SCHEMA monitoring TO uw_grafana;
GRANT SELECT ON monitoring."AgentCostDaily", monitoring."AgentRunOutcomes", monitoring."ProposalStatusDaily",
    monitoring."MappingAuthorship", monitoring."RevisionActivityDaily" TO uw_grafana;
