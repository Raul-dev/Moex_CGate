FROM logstash:8.5.3 

RUN rm -f /usr/share/logstash/pipeline/logstash.conf

USER root 
COPY postgresql-42.2.8.jar /usr/share/logstash/logstash-core/lib/jars/postgresql.jar
COPY mssql-jdbc-12.8.1.jre11.jar /usr/share/logstash/logstash-core/lib/jars/mssql-jdbc.jar