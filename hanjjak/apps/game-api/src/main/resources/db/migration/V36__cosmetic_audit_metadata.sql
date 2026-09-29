alter table cosmetic_audit_event add column algorithm_version varchar(32);
alter table cosmetic_audit_event add column key_id varchar(128);
alter table cosmetic_audit_event add column reproduction_token varchar(128);
alter table cosmetic_audit_event add column details_json jsonb;
